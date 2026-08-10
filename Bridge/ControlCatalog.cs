namespace SolarSim.Bridge;

/// <summary>
/// Fonte única dos atalhos. A ajuda in-game lê as linhas daqui; README e AGENTS.md
/// são conferidos por teste contra os mesmos tokens — assim as três listas não
/// divergem em silêncio.
/// </summary>
public static class ControlCatalog
{
    /// <summary>
    /// Linhas do painel H, na ordem em que aparecem. Editar só aqui.
    /// </summary>
    public static readonly string[] HelpLines =
    [
        "Espaço: pausa e retoma",
        "Setas: dobra e divide a velocidade",
        "R: volta para J2000",
        "Tab / Shift+Tab: ancora no corpo seguinte ou anterior",
        "Clique: ancora no corpo apontado",
        "L: alterna escala logarítmica e linear",
        "N: mostra ou esconde os nomes",
        "Home: devolve a vista inicial (solta a âncora)",
        "Roda: zoom",
        "Botão direito: orbita a câmera",
        "Botão do meio / Shift+direito: arrasta",
        "P / Shift+P: solta uma sonda em órbita ou em fuga",
        "T: preview Δv Terra→Marte    Shift+T: aplica partida na sonda",
        "Delete: descarta a sonda ancorada",
        "F5 / F9: salva e carrega",
        "I: análise ambiental do corpo ancorado",
        "H: mostra ou esconde esta ajuda",
        "Passe o mouse nos rótulos do inspetor para o glossário",
    ];

    /// <summary>
    /// Resumo de uma linha do README. Tem de bater exatamente com a seção Controles.
    /// </summary>
    public const string ReadmeSummary =
        "Espaço pausa · setas velocidade · R J2000 · Tab âncora · L escala · N nomes · Home vista "
        + "· H ajuda · I ensino · P / Shift+P sonda · T / Shift+T Terra→Marte · Delete descarta · "
        + "F5 / F9 save/load · direito orbita · meio / Shift+direito arrasta · roda zoom.";

    /// <summary>
    /// Fragmentos que o parágrafo de controles do AGENTS.md precisa citar. São as
    /// teclas e gestos, não a frase completa — o AGENTS escreve em prosa.
    /// </summary>
    public static readonly string[] AgentsMustMention =
    [
        "Espaço",
        "Tab",
        "Shift+Tab",
        "Home",
        "lista de atalhos",
        "I abre",
        "botão direito",
        "Shift+P",
        "Shift+T",
        "Delete",
        "F5",
        "F9",
    ];
}
