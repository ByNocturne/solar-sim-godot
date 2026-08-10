using SolarSim.Engine.Core;
using SolarSim.Engine.Data;
using SolarSim.Engine.Models;

namespace SolarSim.Engine;

/// <summary>
/// Fachada do motor: junta o relógio, os dados e o propagador, e publica o estado
/// resultante. Nada aqui conhece a camada gráfica.
/// </summary>
/// <remarks>
/// Os corpos vêm de duas origens. Os do repositório são fixos: chegam na construção,
/// nunca trocam de pai e a órbita deles é a que o arquivo declara. Os dinâmicos entram e
/// saem em tempo de execução, carregam uma <see cref="Trajectory"/> em vez de uma órbita
/// só, e são os únicos que a emenda de cônicas reatribui.
/// </remarks>
public sealed class SimEngine
{
    private readonly List<CelestialBodyData> _bodies;

    // Um corpo dinâmico é exatamente um corpo com trajetória: o dicionário é ao mesmo
    // tempo o registro do que foi acrescentado em runtime e o histórico de arcos de cada
    // um.
    private readonly Dictionary<string, Trajectory> _trajectories =
        new(StringComparer.Ordinal);

    private BodyHierarchy _hierarchy;

    // Parâmetro gravitacional efetivo da órbita vigente de cada corpo, indexado como a
    // hierarquia.
    private double[] _mu;

    // Taxa secular total de cada corpo: a declarada no arquivo mais a que a física
    // impõe. Calculada uma vez por remontagem, e não a cada quadro, porque só muda
    // quando a órbita ou o pai mudam.
    private OrbitalElementRates[] _rates;

    // Reaproveitados a cada quadro: o caminho de propagação roda a 60 Hz e não deve
    // gerar lixo para o coletor.
    private StateVector[] _globals;
    private BodyState[] _states;

    public SimEngine(IBodyRepository repository, TimeEngine? time = null)
    {
        ArgumentNullException.ThrowIfNull(repository);

        Time = time ?? new TimeEngine();
        _bodies = [.. repository.LoadBodies()];

        _hierarchy = BodyHierarchy.Create(_bodies);
        _mu = new double[_hierarchy.Count];
        _rates = new OrbitalElementRates[_hierarchy.Count];
        _globals = new StateVector[_hierarchy.Count];
        _states = new BodyState[_hierarchy.Count];

        Rebuild();
        Publish();
    }

    public TimeEngine Time { get; }

    /// <summary>Corpos em ordem de avaliação: o pai sempre antes do filho.</summary>
    public IReadOnlyList<CelestialBodyData> Bodies => _hierarchy.InEvaluationOrder;

    /// <summary>
    /// Corpo na origem do sistema. É a referência das distâncias heliocêntricas, e o
    /// nome dele é o que rotula essa distância na tela.
    /// </summary>
    public CelestialBodyData Root => _hierarchy.Root;

    /// <summary>Dados estáticos de um corpo.</summary>
    /// <exception cref="KeyNotFoundException">Se o identificador não existir.</exception>
    public CelestialBodyData BodyOf(string bodyId) => _hierarchy.Get(bodyId);

    public bool Contains(string bodyId) => _hierarchy.Contains(bodyId);

    /// <summary>Publicado a cada avanço de tempo.</summary>
    public event Action<SystemStateSnapshot>? SystemUpdated;

    /// <summary>
    /// Publicado quando a lista de corpos muda, ou quando um corpo dinâmico troca de
    /// pai. É o aviso de que quem espelha a árvore precisa remontá-la; o evento de
    /// quadro sozinho não diria isso.
    /// </summary>
    public event Action? StructureChanged;

    public void Advance(double realSecondsElapsed)
    {
        Time.Advance(realSecondsElapsed);
        UpdateAttractors();
        Publish();
    }

    /// <summary>
    /// Posição de um corpo em um instante arbitrário, sem mexer no relógio. É o que
    /// permite desenhar órbitas e consultar datas futuras.
    /// </summary>
    public Vector3D PositionAt(string bodyId, double julianDate)
        => StateAt(bodyId, julianDate).PositionKm;

    /// <summary>
    /// Posição e velocidade no referencial global, com a cadeia de pais já composta:
    /// a Lua carrega o movimento da Terra em torno do Sol.
    /// </summary>
    public StateVector StateAt(string bodyId, double julianDate)
        => GlobalStateOf(_hierarchy.IndexOf(bodyId), julianDate - AstroConstants.J2000);

    /// <summary>
    /// Posição e velocidade relativas ao corpo pai, que é o referencial em que os
    /// elementos orbitais são definidos. Para a raiz, o estado é nulo.
    /// </summary>
    public StateVector LocalStateAt(string bodyId, double julianDate)
        => LocalStateOf(_hierarchy.IndexOf(bodyId), julianDate - AstroConstants.J2000);

    /// <summary>
    /// Elementos da órbita vigente de um corpo referidos a J2000, ou nulo se ele for a
    /// raiz. É o que o arquivo declara; para o que a órbita é hoje, ver
    /// <see cref="ElementsAt"/>.
    /// </summary>
    public OrbitalElements? ElementsOf(string bodyId) => _hierarchy.Get(bodyId).Elements;

    /// <summary>
    /// Os elementos como estão em uma data, com as taxas seculares já aplicadas e a
    /// anomalia média referida a essa mesma data.
    /// </summary>
    public OrbitalElements? ElementsAt(string bodyId, double julianDate)
    {
        var index = _hierarchy.IndexOf(bodyId);
        var body = _hierarchy[index];

        if (body.Elements is not { } elements)
        {
            return null;
        }

        // Para um corpo dinâmico quem manda é o arco daquela data. Os elementos do arco
        // guardam a anomalia média referida a J2000 — a mesma convenção da conversão
        // inversa —, então aqui ela é avançada até a data pedida. Sem isso o inspetor
        // mostraria a anomalia de J2000 como se fosse a de agora.
        if (_trajectories.TryGetValue(body.Id, out var trajectory))
        {
            var arc = trajectory.At(julianDate);
            var daysSinceEpoch = julianDate - AstroConstants.J2000;
            var mu = EffectiveMu(arc.ParentId, body.MuKm3S2);
            var meanAnomalyNow = KeplerPropagator.MeanAnomalyAt(
                arc.Elements, mu, daysSinceEpoch);

            return arc.Elements with { MeanAnomalyAtEpochRad = meanAnomalyNow };
        }

        return SecularPropagator.ElementsAt(
            elements, _rates[index], _mu[index], julianDate - AstroConstants.J2000);
    }

    /// <summary>
    /// Quanto os elementos deste corpo andam por segundo, somando o que o arquivo
    /// declara e o que a física impõe. Tudo zero significa órbita fixa.
    /// </summary>
    public OrbitalElementRates SecularRatesOf(string bodyId)
        => _rates[_hierarchy.IndexOf(bodyId)];

    /// <summary>
    /// Parâmetro gravitacional que rege a órbita vigente deste corpo, em km³/s². Vale
    /// zero para a raiz.
    /// </summary>
    public double GravitationalParameterOf(string bodyId)
        => _mu[_hierarchy.IndexOf(bodyId)];

    /// <summary>
    /// Raio da esfera de influência de um corpo, em km. Vale zero para quem não tem
    /// massa e infinito para a raiz, que domina tudo o que não estiver dentro da esfera
    /// de outro.
    /// </summary>
    public double SphereOfInfluenceKm(string bodyId)
        => SphereOfInfluenceOf(_hierarchy.IndexOf(bodyId));

    /// <summary>
    /// Os dois limites de Roche deste corpo em relação ao pai, e o que a maré do pai faz
    /// com ele onde ele passa. Tudo zero para a raiz e para quem não tem massa ou raio.
    /// </summary>
    /// <remarks>
    /// A pergunta é sempre sobre o par: um corpo não tem limite de Roche sozinho, e sim
    /// contra o pai que o puxa. Por isso a consulta é pelo satélite e não pelo planeta,
    /// ao contrário da zona de anel.
    /// </remarks>
    public SatelliteTides TidesOn(string bodyId, double julianDate)
    {
        var body = _hierarchy.Get(bodyId);

        if (body.ParentId is not { } parentId || body.RadiusKm <= 0.0 || body.MuKm3S2 <= 0.0)
        {
            return default;
        }

        // O GM do pai, e não o que rege a órbita deste corpo: para a Lua, o segundo é o do
        // Sol, que é quem rege a órbita da Terra que ela acompanha.
        var parentMu = _hierarchy.Get(parentId).MuKm3S2;

        var rigid = RocheLimit.RigidKm(body.RadiusKm, body.MuKm3S2, parentMu);
        var fluid = RocheLimit.FluidKm(body.RadiusKm, body.MuKm3S2, parentMu);

        // O periápside da órbita de hoje, e não o de J2000: com a precessão e as taxas, a
        // distância de maior aproximação é do instante consultado.
        var periapsisKm = ElementsAt(bodyId, julianDate) is { } elements
            ? elements.PeriapsisKm
            : 0.0;

        return new SatelliteTides
        {
            RigidLimitKm = rigid,
            FluidLimitKm = fluid,
            PeriapsisKm = periapsisKm,
            Fate = RocheLimit.FateAt(periapsisKm, rigid, fluid),
        };
    }

    /// <summary>
    /// A faixa em que este corpo poderia ter um anel, e se poderia. Consultada pelo
    /// hospedeiro: quem tem anel é o planeta, não o escombro.
    /// </summary>
    /// <remarks>
    /// A distância que entra na linha de gelo é o semi-eixo maior, e não a de hoje. Ter
    /// anel é propriedade do corpo, e não do mês: com a distância instantânea, Ceres
    /// cruzaria a linha duas vezes por volta e o veredito piscaria enquanto o tempo corre.
    /// </remarks>
    public RingZone RingZoneOf(string bodyId)
    {
        var body = _hierarchy.Get(bodyId);

        return RingEvaluator.Evaluate(
            body.MuKm3S2,
            body.RadiusKm,
            body.ParentId == Root.Id,
            HeliocentricSemiMajorAxisAu(body));
    }

    /// <summary>
    /// O semi-eixo maior em torno da raiz do corpo ou do planeta que o carrega. Titã está
    /// tão além da linha de gelo quanto Saturno, e responder zero para uma lua faria o
    /// relatório recusá-la pela razão errada.
    /// </summary>
    private double HeliocentricSemiMajorAxisAu(CelestialBodyData body)
    {
        var current = body;

        while (current.ParentId is { } parentId && parentId != Root.Id)
        {
            current = _hierarchy.Get(parentId);
        }

        return current.Elements is { } elements
            ? Math.Abs(elements.SemiMajorAxisKm) / AstroConstants.AstronomicalUnitKm
            : 0.0;
    }

    /// <summary>Identificadores dos corpos acrescentados em runtime.</summary>
    public IReadOnlyCollection<string> DynamicBodyIds => _trajectories.Keys;

    public bool IsDynamic(string bodyId) => _trajectories.ContainsKey(bodyId);

    /// <summary>
    /// Os arcos que compõem a trajetória de um corpo dinâmico, em ordem cronológica.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Se o corpo não for dinâmico.</exception>
    public IReadOnlyList<TrajectoryArc> TrajectoryOf(string bodyId)
        => _trajectories.TryGetValue(bodyId, out var trajectory)
            ? trajectory.Arcs
            : throw new KeyNotFoundException(
                $"O corpo '{bodyId}' não foi acrescentado em runtime e não tem trajetória "
                    + "em arcos: a órbita dele é a que o arquivo de dados declara.");

    /// <summary>
    /// Acrescenta um corpo cuja órbita já vem em elementos, a partir do instante
    /// corrente do relógio.
    /// </summary>
    public void Add(CelestialBodyData body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.ParentId is not { } parentId || body.Elements is not { } elements)
        {
            throw new SystemDataException(
                $"Corpo '{body.Id}': acrescentar em runtime exige 'parent' e órbita. Um "
                    + "segundo corpo sem pai seria uma segunda raiz.");
        }

        Register(body, new Trajectory(new TrajectoryArc(Time.JulianDate, parentId, elements)));
    }

    /// <summary>
    /// Acrescenta um corpo com a trajetória inteira, arcos anteriores inclusive. É por
    /// aqui que um jogo salvo volta: o histórico de emendas não pode ser redescoberto,
    /// porque depende de por onde o corpo passou.
    /// </summary>
    public void Add(CelestialBodyData body, Trajectory trajectory)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(trajectory);

        Register(
            body with
            {
                ParentId = trajectory.Current.ParentId,
                Elements = trajectory.Current.Elements,
            },
            trajectory);
    }

    /// <summary>
    /// Acrescenta um corpo a partir de onde ele está e para onde vai. É o caminho de uma
    /// nave: a órbita não é escolhida, e sim consequência do vetor de estado.
    /// </summary>
    /// <param name="body">
    /// Identidade e dados físicos. O campo de órbita é ignorado; quem manda é o estado.
    /// </param>
    /// <param name="localState">Posição e velocidade relativas ao pai declarado.</param>
    /// <param name="julianDate">Instante a que o estado se refere.</param>
    public void AddFromState(
        CelestialBodyData body,
        in StateVector localState,
        double julianDate)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.ParentId is not { } parentId)
        {
            throw new SystemDataException(
                $"Corpo '{body.Id}': acrescentar em runtime exige 'parent'.");
        }

        if (!Contains(parentId))
        {
            throw new SystemDataException(
                $"Corpo '{body.Id}': o pai '{parentId}' não existe no sistema.");
        }

        var elements = OrbitDetermination.ElementsFrom(
            localState,
            EffectiveMu(parentId, body.MuKm3S2),
            julianDate - AstroConstants.J2000);

        Register(
            body with { Elements = elements },
            new Trajectory(new TrajectoryArc(julianDate, parentId, elements)));
    }

    /// <summary>
    /// Remove um corpo. Recusa a raiz e quem tem filhos, porque as duas coisas deixariam
    /// o sistema sem hierarquia válida.
    /// </summary>
    /// <returns>Falso se o corpo não existia.</returns>
    public bool Remove(string bodyId)
    {
        if (!Contains(bodyId))
        {
            return false;
        }

        if (_hierarchy.Root.Id == bodyId)
        {
            throw new SystemDataException(
                $"Corpo '{bodyId}': é a raiz do sistema, e removê-la deixaria todo o resto "
                    + "sem origem.");
        }

        var filhos = _bodies
            .Where(body => body.ParentId == bodyId)
            .Select(body => body.Id)
            .ToArray();

        if (filhos.Length > 0)
        {
            throw new SystemDataException(
                $"Corpo '{bodyId}': ainda tem {string.Join(", ", filhos)} orbitando. "
                    + "Remova os filhos primeiro.");
        }

        _bodies.RemoveAll(body => body.Id == bodyId);
        _trajectories.Remove(bodyId);

        Rebuild();
        StructureChanged?.Invoke();
        Publish();

        return true;
    }

    private void Register(CelestialBodyData body, Trajectory trajectory)
    {
        var anterior = _bodies.ToList();

        var trajetoriaAnterior =
            _trajectories.TryGetValue(body.Id, out var existente) ? existente : null;

        _bodies.Add(body);

        // A trajetória entra antes da remontagem porque é ela que identifica o corpo como
        // dinâmico, e a remontagem calcula as taxas seculares — que um corpo dinâmico não
        // tem. Registrada depois, a sonda nasceria precessando pelo achatamento do pai.
        _trajectories[body.Id] = trajectory;

        try
        {
            // A validação de identificador duplicado, pai inexistente e ciclo é a mesma
            // da carga do arquivo, e vale a pena ser: um corpo criado em runtime não
            // merece verificação mais frouxa do que um vindo do JSON.
            Rebuild();
        }
        catch
        {
            _bodies.Clear();
            _bodies.AddRange(anterior);

            if (trajetoriaAnterior is null)
            {
                _trajectories.Remove(body.Id);
            }
            else
            {
                _trajectories[body.Id] = trajetoriaAnterior;
            }

            Rebuild();
            throw;
        }

        StructureChanged?.Invoke();
        Publish();
    }

    private void Rebuild()
    {
        _hierarchy = BodyHierarchy.Create(_bodies);

        if (_mu.Length != _hierarchy.Count)
        {
            _mu = new double[_hierarchy.Count];
            _rates = new OrbitalElementRates[_hierarchy.Count];
            _globals = new StateVector[_hierarchy.Count];
            _states = new BodyState[_hierarchy.Count];
        }

        for (var index = 0; index < _hierarchy.Count; index++)
        {
            _mu[index] = EffectiveMu(index);
        }

        // Em uma segunda passada, porque a taxa de origem física depende do mu efetivo
        // que a passada anterior acabou de calcular.
        for (var index = 0; index < _hierarchy.Count; index++)
        {
            _rates[index] = TotalRatesOf(index);
        }
    }

    /// <summary>
    /// A taxa declarada no arquivo somada à que a física impõe: precessão relativística
    /// do periápside, efeito do achatamento do pai, e — quando o arquivo declara
    /// parâmetros — Yarkovsky e pressão de radiação.
    /// </summary>
    /// <remarks>
    /// Corpo dinâmico não entra: a órbita dele é o arco vigente, obtido de um vetor de
    /// estado por um caminho que assume dois corpos puros. Aplicar taxa ali faria a
    /// conversão de ida e a de volta discordarem.
    /// </remarks>
    private OrbitalElementRates TotalRatesOf(int index)
    {
        var body = _hierarchy[index];

        if (body.Elements is not { } elements
            || _hierarchy.ParentOf(index) is not { } parent
            || _trajectories.ContainsKey(body.Id))
        {
            return OrbitalElementRates.None;
        }

        return body.Rates
            + SecularPerturbations.For(
                elements, _mu[index], parent.J2, parent.J2ReferenceRadiusKm)
            + NonGravitationalDrift.For(elements, _mu[index], body.NonGravitational);
    }

    /// <summary>
    /// A equação do movimento relativo de dois corpos usa a soma dos dois parâmetros
    /// gravitacionais, não só o do corpo central. Para um planeta em torno do Sol a
    /// diferença é imperceptível; para a Lua em torno da Terra vale 1,2%, o suficiente
    /// para deslocar o mês sideral em quatro horas.
    /// </summary>
    private double EffectiveMu(int index)
        => _hierarchy.ParentOf(index) is { } parent
            ? parent.MuKm3S2 + _hierarchy[index].MuKm3S2
            : 0.0;

    private double EffectiveMu(string parentId, double bodyMuKm3S2)
        => _hierarchy.Get(parentId).MuKm3S2 + bodyMuKm3S2;

    /// <summary>
    /// Uma passada só, na ordem de avaliação: quando um corpo é processado, o estado
    /// global do pai dele já está pronto na mesma tabela.
    /// </summary>
    private void Publish()
    {
        if (SystemUpdated is null)
        {
            return;
        }

        var daysSinceEpoch = Time.DaysSinceEpoch;

        for (var index = 0; index < _hierarchy.Count; index++)
        {
            var parentIndex = ParentIndexOf(index, daysSinceEpoch);
            var local = LocalStateOf(index, daysSinceEpoch);

            _globals[index] = parentIndex switch
            {
                < 0 => local,

                // O caminho normal: o pai vem antes na ordem de avaliação e já está
                // pronto. A exceção é o corpo dinâmico consultado numa data em que ele
                // tinha outro pai, que pode estar depois dele na ordem de hoje.
                _ when parentIndex < index => _globals[parentIndex] + local,

                _ => GlobalStateOf(parentIndex, daysSinceEpoch) + local,
            };

            _states[index] = new BodyState(
                _hierarchy[index].Id, _globals[index].PositionKm, local.PositionKm);
        }

        SystemUpdated.Invoke(new SystemStateSnapshot(Time.JulianDate, _states));
    }

    /// <summary>
    /// P_global(A) = P_global(Pai(A)) + P_local(A). Fora do laço de quadro a composição
    /// é recursiva, porque a profundidade da árvore é pequena e a clareza compensa.
    /// </summary>
    private StateVector GlobalStateOf(int index, double daysSinceEpoch)
    {
        var local = LocalStateOf(index, daysSinceEpoch);
        var parentIndex = ParentIndexOf(index, daysSinceEpoch);

        return parentIndex < 0
            ? local
            : GlobalStateOf(parentIndex, daysSinceEpoch) + local;
    }

    private StateVector LocalStateOf(int index, double daysSinceEpoch)
    {
        var body = _hierarchy[index];

        if (_trajectories.TryGetValue(body.Id, out var trajectory))
        {
            var arc = trajectory.At(AstroConstants.J2000 + daysSinceEpoch);

            return KeplerPropagator.StateAt(
                arc.Elements,
                EffectiveMu(arc.ParentId, body.MuKm3S2),
                daysSinceEpoch);
        }

        return body.Elements is { } elements
            ? SecularPropagator.StateAt(elements, _rates[index], _mu[index], daysSinceEpoch)
            : default;
    }

    /// <summary>
    /// Quem atraía o corpo naquele instante. Para um corpo do arquivo é sempre o mesmo;
    /// para um dinâmico é o pai do arco vigente, que pode não ser o de hoje.
    /// </summary>
    private int ParentIndexOf(int index, double daysSinceEpoch)
    {
        if (_trajectories.TryGetValue(_hierarchy[index].Id, out var trajectory))
        {
            return _hierarchy.IndexOf(
                trajectory.At(AstroConstants.J2000 + daysSinceEpoch).ParentId);
        }

        return _hierarchy.ParentIndices[index];
    }

    private double SphereOfInfluenceOf(int index)
    {
        var parentIndex = _hierarchy.ParentIndices[index];

        if (parentIndex < 0)
        {
            return double.PositiveInfinity;
        }

        var body = _hierarchy[index];

        return body.Elements is { } elements
            ? SphereOfInfluence.RadiusKm(
                elements.SemiMajorAxisKm,
                body.MuKm3S2,
                _hierarchy[parentIndex].MuKm3S2)
            : 0.0;
    }

    /// <summary>
    /// Revê, para cada corpo dinâmico, quem o atrai — e emenda um arco novo quando a
    /// resposta muda.
    /// </summary>
    private void UpdateAttractors()
    {
        if (_trajectories.Count == 0)
        {
            return;
        }

        var julianDate = Time.JulianDate;
        var trocou = false;

        foreach (var bodyId in _trajectories.Keys.ToArray())
        {
            var trajectory = _trajectories[bodyId];
            var vigente = trajectory.Current;

            // Com o tempo andando para trás, a emenda que ainda não aconteceu deixa de
            // existir; será redescoberta se o tempo voltar a passar por aqui.
            trajectory.RewindTo(julianDate);

            if (trajectory.Current != vigente)
            {
                Adopt(bodyId, trajectory.Current);
                trocou = true;
            }

            var atual = trajectory.Current.ParentId;

            // Em um único Advance a sonda pode atravessar mais de uma esfera (Lua →
            // Terra → Sol). Sem o laço, cada quadro só sobe ou desce um nível, e com o
            // tempo acelerado a captura intermediária some.
            for (var hop = 0; hop < MaxAttractorHopsPerAdvance; hop++)
            {
                var index = _hierarchy.IndexOf(bodyId);
                var dominante = DominantAttractor(index, atual, julianDate);

                if (dominante == atual)
                {
                    break;
                }

                Reparent(bodyId, dominante, julianDate);
                atual = dominante;
                trocou = true;
            }
        }

        if (trocou)
        {
            StructureChanged?.Invoke();
        }
    }

    /// <summary>
    /// Teto de trocas de atrator por corpo e por <see cref="Advance"/>. Cobre a cadeia
    /// Lua→Terra→Sol com folga; um número maior esconderia um ciclo patológico.
    /// </summary>
    private const int MaxAttractorHopsPerAdvance = 8;

    /// <summary>
    /// Qual corpo domina a atração sobre este, agora: o pai de sempre, o avô — se o
    /// corpo saiu da esfera de influência do pai — ou um irmão em cuja esfera ele
    /// entrou.
    /// </summary>
    private string DominantAttractor(int index, string currentParentId, double julianDate)
    {
        var parentIndex = _hierarchy.IndexOf(currentParentId);
        var position = GlobalStateOf(index, julianDate - AstroConstants.J2000).PositionKm;

        var parentPosition = GlobalStateOf(parentIndex, julianDate - AstroConstants.J2000)
            .PositionKm;

        if ((position - parentPosition).Magnitude > SphereOfInfluenceOf(parentIndex)
            && _hierarchy.ParentOf(parentIndex) is { } grandparent)
        {
            return grandparent.Id;
        }

        for (var candidate = 0; candidate < _hierarchy.Count; candidate++)
        {
            if (candidate == index || _hierarchy.ParentIndices[candidate] != parentIndex)
            {
                continue;
            }

            var radius = SphereOfInfluenceOf(candidate);

            if (radius <= 0.0)
            {
                continue;
            }

            var candidatePosition =
                GlobalStateOf(candidate, julianDate - AstroConstants.J2000).PositionKm;

            if ((position - candidatePosition).Magnitude < radius)
            {
                return _hierarchy[candidate].Id;
            }
        }

        return currentParentId;
    }

    /// <summary>
    /// A emenda propriamente dita: o estado relativo ao novo pai, medido no instante da
    /// troca, vira os elementos do arco seguinte. Por sair do mesmo vetor de estado, a
    /// posição e a velocidade no referencial global não dão nenhum salto — o que muda é
    /// só quem passa a ser considerado responsável pela curva.
    /// </summary>
    private void Reparent(string bodyId, string newParentId, double julianDate)
    {
        var index = _hierarchy.IndexOf(bodyId);
        var body = _hierarchy[index];
        var daysSinceEpoch = julianDate - AstroConstants.J2000;

        var relative = GlobalStateOf(index, daysSinceEpoch)
            - GlobalStateOf(_hierarchy.IndexOf(newParentId), daysSinceEpoch);

        var elements = OrbitDetermination.ElementsFrom(
            relative, EffectiveMu(newParentId, body.MuKm3S2), daysSinceEpoch);

        var arc = new TrajectoryArc(julianDate, newParentId, elements);

        _trajectories[bodyId].Append(arc);
        Adopt(bodyId, arc);
    }

    /// <summary>
    /// Aplica um impulso instantâneo a um corpo dinâmico: a posição fica, a velocidade
    /// salta, e o arco vigente é emendado por outro com os elementos novos. É o que
    /// transforma um preview de transferência em trajetória.
    /// </summary>
    /// <param name="deltaVKmS">
    /// Incremento de velocidade no referencial do pai atual, em km/s.
    /// </param>
    public void ApplyImpulse(string bodyId, in Vector3D deltaVKmS, double julianDate)
    {
        if (!_trajectories.ContainsKey(bodyId))
        {
            throw new SystemDataException(
                $"Corpo '{bodyId}': impulso só se aplica a corpo dinâmico. Solte uma "
                    + "sonda antes.");
        }

        var index = _hierarchy.IndexOf(bodyId);
        var body = _hierarchy[index];
        var daysSinceEpoch = julianDate - AstroConstants.J2000;
        var parentId = _trajectories[bodyId].At(julianDate).ParentId;

        var local = LocalStateOf(index, daysSinceEpoch);
        var after = new StateVector(
            local.PositionKm,
            local.VelocityKmS + deltaVKmS);

        var elements = OrbitDetermination.ElementsFrom(
            after, EffectiveMu(parentId, body.MuKm3S2), daysSinceEpoch);

        var arc = new TrajectoryArc(julianDate, parentId, elements);

        _trajectories[bodyId].Append(arc);
        Adopt(bodyId, arc);

        StructureChanged?.Invoke();
        Publish();
    }

    /// <summary>
    /// Substitui o estado local de um corpo dinâmico (posição e velocidade relativas ao
    /// pai atual) e emenda um arco novo. Usado pela partida de transferência, que precisa
    /// colocar a sonda fora da SOI de origem com a velocidade de Lambert.
    /// </summary>
    public void SetLocalState(string bodyId, in StateVector localState, double julianDate)
    {
        if (!_trajectories.ContainsKey(bodyId))
        {
            throw new SystemDataException(
                $"Corpo '{bodyId}': só corpo dinâmico aceita estado imposto em runtime.");
        }

        var index = _hierarchy.IndexOf(bodyId);
        var body = _hierarchy[index];
        var daysSinceEpoch = julianDate - AstroConstants.J2000;
        var parentId = _trajectories[bodyId].At(julianDate).ParentId;

        var elements = OrbitDetermination.ElementsFrom(
            localState, EffectiveMu(parentId, body.MuKm3S2), daysSinceEpoch);

        var arc = new TrajectoryArc(julianDate, parentId, elements);

        _trajectories[bodyId].Append(arc);
        Adopt(bodyId, arc);

        StructureChanged?.Invoke();
        Publish();
    }

    /// <summary>
    /// Faz a árvore refletir o arco vigente de um corpo dinâmico. A descrição estática
    /// dele guarda sempre a órbita de agora, para que quem consulta o motor — o inspetor,
    /// a árvore do sistema, o desenho da órbita — não precise saber que existem arcos.
    /// </summary>
    private void Adopt(string bodyId, TrajectoryArc arc)
    {
        var position = _bodies.FindIndex(candidate => candidate.Id == bodyId);

        _bodies[position] = _bodies[position] with
        {
            ParentId = arc.ParentId,
            Elements = arc.Elements,
        };

        Rebuild();
    }
}
