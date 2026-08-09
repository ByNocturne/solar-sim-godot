namespace SolarSim.Engine.Models;

/// <summary>
/// O que a maré do corpo pai faz com um satélite, dado onde ele passa.
/// </summary>
/// <remarks>
/// Três estados e não dois porque os dois limites de Roche não coincidem: entre o rígido
/// e o fluido está a faixa em que a resposta depende de do que o corpo é feito, e
/// achatá-la em "sobrevive" ou "não sobrevive" seria afirmar o que o modelo não sabe.
/// Fobos está exatamente nessa faixa.
/// </remarks>
public enum SatelliteFate
{
    /// <summary>Falta massa, raio ou órbita para responder. É o caso de uma sonda.</summary>
    Unknown = 0,

    /// <summary>Passa fora do limite fluido: a gravidade própria vence a maré.</summary>
    Stable,

    /// <summary>
    /// Entre os dois limites. Um corpo coeso aguenta; uma pilha de escombros se alonga e
    /// se desfaz aos poucos.
    /// </summary>
    AtRisk,

    /// <summary>Dentro do limite rígido: nem um corpo coeso se segura pela gravidade.</summary>
    Disrupted,
}
