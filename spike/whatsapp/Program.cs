// Prova do WhatsApp (DESCARTÁVEL). Responde: dá para o Vitral abrir uma conversa do WhatsApp Desktop pelo nome,
// escrever e enviar, por UI Automation, sem atrapalhar quem está usando o computador?
//
//   arvore [prof] [max]              despeja a árvore UIA3 (cuidado: sai com conteúdo de conversas; não commitar)
//   lista                            nomes das conversas visíveis na lista
//   abrir <nome>                     abre a conversa pela busca e mostra o que achou (não envia nada)
//   enviar <nome> <texto> [modo]     abre, escreve e envia; modo = fundo (padrão) | frente
//   rodada <nome> <n> [modo]         n envios "teste Vitral i/n" com tempo e resultado (CSV em resultados.csv)
//
// Restaura a janela como estava (minimizada, em segundo plano, primeiro plano) e devolve o foco a quem tinha.
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

using var uia = new UIA3Automation();

Process? Proc() => Process.GetProcessesByName("WhatsApp.Root").FirstOrDefault(p => p.MainWindowHandle != 0);

string Seg(Func<string?> f) { try { return f() ?? ""; } catch { return "?"; } }

AutomationElement Raiz()
{
    // A janela principal pode sumir do MainWindowHandle quando minimizada para a bandeja; procura pela classe.
    var p = Proc();
    nint h = p?.MainWindowHandle ?? 0;
    if (h == 0)
    {
        foreach (var q in Process.GetProcessesByName("WhatsApp.Root"))
            EnumWindows((w, _) => { GetWindowThreadProcessId(w, out uint pid); if (pid == q.Id && Classe(w) == "WinUIDesktopWin32WindowClass") { h = w; return false; } return true; }, 0);
    }
    if (h == 0) throw new Falha("janela", "WhatsApp não está aberto");
    return uia.FromHandle(h);
}

AutomationElement Documento(AutomationElement raiz) =>
    Primeiro(raiz, PorId("RootWebArea"))
    ?? throw new Falha("documento", "WebView sem árvore de acessibilidade");

if (args is ["arvore", ..])
{
    int prof = args.Length > 1 ? int.Parse(args[1]) : 40, max = args.Length > 2 ? int.Parse(args[2]) : 800, n = 0;
    var w = uia.TreeWalkerFactory.GetRawViewWalker();
    void Ver(AutomationElement e, int d)
    {
        if (n >= max || d > prof) return;
        n++;
        var nome = Seg(() => e.Name); if (nome.Length > 80) nome = nome[..80] + "…";
        Console.WriteLine($"{new string(' ', d * 2)}{Seg(() => e.ControlType.ToString())} '{nome.Replace("\n", "⏎")}' id={Seg(() => e.AutomationId)}");
        for (var f = w.GetFirstChild(e); f != null; f = w.GetNextSibling(f)) Ver(f, d + 1);
    }
    Ver(args.Contains("main") ? Primeiro(Documento(Raiz()), PorId("main")) ?? Raiz() : Raiz(), 0);
    return 0;
}

if (args is ["padroes", var nomeP])
{
    var doc = Documento(Raiz());
    var grade = Primeiro(Primeiro(doc, PorId("pane-side")), PorTipo(ControlType.DataGrid));
    var item = grade?.FindAllChildren().FirstOrDefault(i => Titulo(i) == nomeP);
    if (item is null) { Console.WriteLine("não achei"); return 1; }
    foreach (var e in new[] { item }.Concat(Andar(item, 40)))
        Console.WriteLine($"{Seg(() => e.ControlType.ToString())} '{Seg(() => e.Name)}' foco={Seg(() => e.Properties.IsKeyboardFocusable.ValueOrDefault.ToString())} padrões={string.Join(",", e.GetSupportedPatterns().Select(p => p.Name))}");
    return 0;
}

if (args is ["buscar", var termo])
{
    var doc = Documento(Raiz());
    var busca = Primeiro(Primeiro(doc, PorId("side")), PorTipo(ControlType.Edit))!;
    busca.Patterns.Value.Pattern.SetValue(termo);
    Thread.Sleep(1500);
    var grade = Primeiro(Primeiro(doc, PorId("pane-side")), PorTipo(ControlType.DataGrid));
    foreach (var item in grade?.FindAllChildren() ?? []) Console.WriteLine($"[{Titulo(item)}]");
    busca.Patterns.Value.Pattern.SetValue("");
    return 0;
}

if (args is ["com"])
{
    // 2(a): o ativador de notificações do WhatsApp (ToastActivatorCLSID do manifesto) aceita outro processo? Só cria o
    // objeto e pede a interface; NÃO chama Activate (isso seria responder de fato).
    var clsid = new Guid("bebd6a5e-1b14-4a57-a195-c772d28b6d15");
    var iid = new Guid("53E31837-6600-4A81-9395-75CFFE746F94"); // INotificationActivationCallback
    int hr = CoCreateInstance(ref clsid, 0, 4 /* CLSCTX_LOCAL_SERVER */, ref iid, out nint ptr);
    Console.WriteLine($"CoCreateInstance(ativador do WhatsApp) = 0x{hr:X8} ptr={(ptr != 0 ? "sim" : "não")}");
    if (ptr != 0) Marshal.Release(ptr);
    return 0;
}

if (args is ["responder-notif", var argumentos, var resposta])
{
    // 2(a'): faz o que o Windows faz quando a pessoa digita na notificação e aperta Enviar: chama o ativador COM do
    // WhatsApp com os argumentos da ação e o texto da caixa. Os argumentos vêm do XML do toast (banco de notificações).
    var clsid = new Guid("bebd6a5e-1b14-4a57-a195-c772d28b6d15");
    var tipo = Type.GetTypeFromCLSID(clsid)!;
    var sw = Stopwatch.StartNew();
    var ativador = (INotificationActivationCallback)Activator.CreateInstance(tipo)!;
    var dados = new[] { new NOTIFICATION_USER_INPUT_DATA { Key = "quickReplyInputBoxId", Value = resposta } };
    ativador.Activate("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", argumentos, dados, (uint)dados.Length);
    Console.WriteLine($"Activate devolveu em {sw.ElapsedMilliseconds} ms");
    Marshal.ReleaseComObject(ativador);
    return 0;
}

if (args is ["lista"])
{
    var doc = Documento(Raiz());
    var grade = Primeiro(Primeiro(doc, PorId("pane-side")), PorTipo(ControlType.DataGrid));
    foreach (var item in grade?.FindAllChildren() ?? [])
        Console.WriteLine(Seg(() => item.Name));
    return 0;
}

if (args is ["abrir", var nomeAbrir])
{
    var r = Executar(nomeAbrir, null, frente: false);
    Console.WriteLine(r);
    return r.Ok ? 0 : 1;
}

if (args is ["enviar", var nome, var texto, ..])
{
    var r = Executar(nome, texto, frente: args.Length > 3 && args[3] == "frente");
    Console.WriteLine(r);
    return r.Ok ? 0 : 1;
}

if (args is ["rodada", var nomeR, var nTxt, ..])
{
    int total = int.Parse(nTxt);
    bool frente = args.Length > 3 && args[3] == "frente";
    string csv = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "resultados.csv");
    if (!File.Exists(csv)) File.WriteAllText(csv, "quando;modo;estado_antes;i;ok;etapa;ms;estado_depois;foco_devolvido;detalhe\n");
    int oks = 0;
    for (int i = 1; i <= total; i++)
    {
        var r = Executar(nomeR, $"teste Vitral {i}/{total} ({DateTime.Now:HH:mm:ss})", frente);
        if (r.Ok) oks++;
        Console.WriteLine($"{i}/{total} {r}");
        File.AppendAllText(csv, $"{DateTime.Now:s};{(frente ? "frente" : "fundo")};{r.EstadoAntes};{i};{r.Ok};{r.Etapa};{r.Ms};{r.EstadoDepois};{r.FocoDevolvido};{r.Detalhe.Replace(';', ',')}\n");
        Thread.Sleep(1500);
    }
    Console.WriteLine($"Acertos: {oks}/{total}");
    return 0;
}

Console.WriteLine("Uso: arvore | lista | abrir <nome> | enviar <nome> <texto> [fundo|frente] | rodada <nome> <n> [fundo|frente]");
return 2;

// ---------------------------------------------------------------------------------------------------------------

Resultado Executar(string nome, string? texto, bool frente)
{
    var sw = Stopwatch.StartNew();
    nint focoAntes = GetForegroundWindow();
    var raiz = Raiz();
    nint hwnd = raiz.Properties.NativeWindowHandle.Value;
    string estadoAntes = Estado(hwnd);
    string etapa = "inicio";
    try
    {
        if (frente)
        {
            etapa = "trazer";
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            Thread.Sleep(300);
        }
        else if (estadoAntes == "minimizada" && Environment.GetEnvironmentVariable("PROVA_SEM_RESTAURAR") == "1") { }
        else if (estadoAntes == "minimizada")
        {
            // Restaurar sem ativar: SW_SHOWNOACTIVATE devolve a janela ao lugar sem tomar o foco.
            etapa = "restaurar-sem-ativar";
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            Thread.Sleep(400);
        }
        else if (estadoAntes == "oculta")
        {
            etapa = "mostrar-sem-ativar";
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            Thread.Sleep(600);
        }

        etapa = "documento";
        var doc = Esperar(() => Documento(raiz), 3000);

        etapa = "busca";
        var lado = Primeiro(doc, PorId("side")) ?? throw new Falha(etapa, "painel lateral não achado");
        var busca = Primeiro(lado, PorTipo(ControlType.Edit)) ?? throw new Falha(etapa, "caixa de busca não achada");
        busca.Patterns.Value.Pattern.SetValue(nome);

        etapa = "resultado";
        // Resultado: um item da lista cujo título (primeiro Text) é exatamente o nome. Nome exato evita "Eu - Escritório" ao buscar "Eu".
        AutomationElement? alvo = null;
        int iguais = 0;
        var limite = Stopwatch.StartNew();
        while (limite.ElapsedMilliseconds < 4000)
        {
            var grade = Primeiro(Primeiro(lado, PorId("pane-side")), PorTipo(ControlType.DataGrid))
                        ?? Primeiro(lado, PorTipo(ControlType.DataGrid));
            var itens = grade?.FindAllChildren() ?? [];
            // Só Conversas e Contatos: a seção Mensagens lista mensagens que contêm o termo, não conversas.
            var secao = itens.TakeWhile(i => Titulo(i) is not ("Mensagens" or "Messages")).ToList();
            var exatos = secao.Where(i => Titulo(i) == nome).ToList();
            if (exatos.Count > 0) { alvo = exatos[0]; iguais = exatos.Count; break; }
            Thread.Sleep(150);
        }
        if (alvo is null) throw new Falha(etapa, $"nenhuma conversa chamada exatamente '{nome}'");

        etapa = "abrir";
        // Select/Invoke não abrem a conversa (só marcam aria-selected). O que abre é a ação padrão do MSAA
        // (DoDefaultAction), que no Chromium vira um clique sintético no elemento, sem mover o mouse nem pedir foco.
        var candidatos = new[] { alvo }.Concat(Andar(alvo, 40))
            .Where(e => Seg(() => e.Properties.IsKeyboardFocusable.ValueOrDefault.ToString()) == "True").ToList();
        if (candidatos.Count == 0) throw new Falha(etapa, "item sem elemento clicável");
        string comoAbriu = "";
        AutomationElement? mainAberto = null;
        bool Aberta(int ms)
        {
            var lim = Stopwatch.StartNew();
            while (lim.ElapsedMilliseconds < ms)
            {
                var m = Primeiro(doc, PorId("main"));
                if (m != null && CabecalhoConfere(m, nome)) { mainAberto = m; return true; }
                Thread.Sleep(150);
            }
            return false;
        }
        // DoDefaultAction (clique sintético do MSAA) não abre: o WhatsApp abre a conversa no mousedown, que esse clique
        // não gera. Medido na prova; ficou de fora para não gastar 800 ms por tentativa.
        // 2) clique postado (PostMessage WM_LBUTTONDOWN/UP) nas janelas filhas que recebem a entrada do WebView2,
        //    sem mexer no mouse de verdade nem trazer a janela para frente.
        if (mainAberto is null)
        {
            var r = candidatos[0].BoundingRectangle;
            var ponto = new PONTO { x = (int)(r.Left + Math.Min(60, r.Width / 2)), y = (int)(r.Top + r.Height / 2) };
            foreach (var (filha, cls) in JanelasFilhas(hwnd))
            {
                if (cls is not ("InputSiteWindowClass" or "Chrome_RenderWidgetHostHWND" or "Chrome_WidgetWin_1" or "Chrome_WidgetWin_0")) continue;
                var p = ponto; ScreenToClient(filha, ref p);
                nint lp = (p.y << 16) | (p.x & 0xFFFF);
                PostMessage(filha, 0x0200, 0, lp);            // WM_MOUSEMOVE
                PostMessage(filha, 0x0201, 1, lp);            // WM_LBUTTONDOWN
                PostMessage(filha, 0x0202, 0, lp);            // WM_LBUTTONUP
                if (Aberta(1000)) { comoAbriu = "PostMessage:" + cls; break; }
            }
        }

        etapa = "conversa";
        var main = mainAberto ?? throw new Falha("conversa", "painel da conversa não abriu com o nome certo no cabeçalho");

        etapa = "caixa";
        var rodape = Primeiro(main, PorId("footer")) ?? main;
        var caixa = Primeiro(rodape, PorTipo(ControlType.Edit)) ?? throw new Falha(etapa, "caixa de mensagem não achada");

        // Limpa a busca para a lista voltar ao normal (como o usuário a deixou).
        try { busca.Patterns.Value.Pattern.SetValue(""); } catch { }

        if (texto is null)
            return new Resultado(true, "aberta", sw.ElapsedMilliseconds, estadoAntes, Devolver(hwnd, estadoAntes, focoAntes, frente), Estado(hwnd),
                $"iguais={iguais} abriu_por={comoAbriu} caixa='{Seg(() => caixa.Name)}' valor='{Seg(() => caixa.Patterns.Value.PatternOrDefault?.Value.Value)}'");

        etapa = "rascunho";
        string rascunho = Seg(() => caixa.Patterns.Value.PatternOrDefault?.Value.Value);
        if (!string.IsNullOrWhiteSpace(rascunho)) throw new Falha(etapa, "a conversa tem rascunho; não sobrescrevo");

        etapa = "escrever";
        // SetValue não chega ao editor (Lexical, contenteditable). Foca a caixa pela UIA e posta WM_CHAR, um por unidade
        // UTF-16, na janela do WebView2 que aceitou o clique.
        string escrito = "";
        string comoEscreveu = "";
        foreach (var (filha, cls) in JanelasFilhas(hwnd).Where(j => j.Item2.StartsWith("Chrome_") || j.Item2 == "InputSiteWindowClass"))
        {
            try { caixa.Focus(); } catch { }
            Thread.Sleep(100);
            foreach (char ch in texto) PostMessage(filha, 0x0102, ch, 1); // WM_CHAR
            var lim3 = Stopwatch.StartNew();
            while (lim3.ElapsedMilliseconds < 1500)
            {
                escrito = Seg(() => caixa.Patterns.Value.Pattern.Value.Value).Trim();
                if (escrito == texto.Trim()) break;
                Thread.Sleep(100);
            }
            Console.Error.WriteLine($"  WM_CHAR em {cls}: caixa='{escrito}'");
            if (escrito == texto.Trim()) { comoEscreveu = cls; break; }
            if (escrito != "") throw new Falha(etapa, $"caixa ficou com '{escrito}' (texto parcial; apague à mão)");
        }
        if (escrito != texto.Trim()) throw new Falha(etapa, $"caixa ficou com '{escrito}'");

        etapa = "enviar";
        var botao = Esperar(() => Todos(rodape, PorTipo(ControlType.Button))
                                       .FirstOrDefault(b => Seg(() => b.Name) is "Enviar" or "Send") ?? throw new Falha("enviar", "botão Enviar não apareceu"), 2000);
        string comoEnviou = "";
        bool Vazia(int ms)
        {
            var l = Stopwatch.StartNew();
            while (l.ElapsedMilliseconds < ms) { if (Seg(() => caixa.Patterns.Value.Pattern.Value.Value).Trim() == "") return true; Thread.Sleep(100); }
            return false;
        }
        try { botao.Patterns.Invoke.Pattern.Invoke(); } catch { }
        if (Vazia(1500)) comoEnviou = "Invoke";
        else
        {
            try { botao.Patterns.LegacyIAccessible.Pattern.DoDefaultAction(); } catch { }
            if (Vazia(1500)) comoEnviou = "DoDefaultAction";
        }
        if (comoEnviou == "")
        {
            var rb = botao.BoundingRectangle;
            var pb = new PONTO { x = (int)(rb.Left + rb.Width / 2), y = (int)(rb.Top + rb.Height / 2) };
            foreach (var (filha, cls) in JanelasFilhas(hwnd).Where(j => j.Item2.StartsWith("Chrome_")))
            {
                var p = pb; ScreenToClient(filha, ref p);
                nint lp = (p.y << 16) | (p.x & 0xFFFF);
                PostMessage(filha, 0x0200, 0, lp); PostMessage(filha, 0x0201, 1, lp); PostMessage(filha, 0x0202, 0, lp);
                if (Vazia(1500)) { comoEnviou = "PostMessage:" + cls; break; }
            }
        }
        Console.Error.WriteLine($"  enviou por: {(comoEnviou == "" ? "nada" : comoEnviou)}");
        if (comoEnviou == "") throw new Falha("enviar", "nenhum jeito de apertar Enviar funcionou; o texto ficou na caixa");

        etapa = "confirmar";
        bool apareceu = false;
        var lim2 = Stopwatch.StartNew();
        while (lim2.ElapsedMilliseconds < 3000 && !apareceu)
        {
            apareceu = Todos(main, PorTipo(ControlType.Text)).Any(t => Seg(() => t.Name).Contains(texto));
            if (!apareceu) Thread.Sleep(150);
        }
        if (!apareceu) throw new Falha(etapa, "texto não apareceu na conversa");

        return new Resultado(true, "enviado", sw.ElapsedMilliseconds, estadoAntes, Devolver(hwnd, estadoAntes, focoAntes, frente), Estado(hwnd), $"iguais={iguais} abriu={comoAbriu} escreveu={comoEscreveu} enviou={comoEnviou}");
    }
    catch (Exception e)
    {
        bool dev = Devolver(hwnd, estadoAntes, focoAntes, frente);
        string det = e is Falha f ? f.Message : $"{e.GetType().Name}: {e.Message}";
        return new Resultado(false, e is Falha f2 ? f2.Etapa : etapa, sw.ElapsedMilliseconds, estadoAntes, dev, Estado(hwnd), det);
    }
}

// A busca nativa do UIA (FindFirst/FindAll com Descendants) entra em laço na árvore do WebView2: o documento aparece
// de novo dentro dele mesmo. Por isso a busca anda pela árvore crua (a de controle esconde os divs com id, como "side" e "main") e pula RuntimeIds já vistos.
Func<AutomationElement, bool> PorId(string id) => e => Seg(() => e.AutomationId) == id;
Func<AutomationElement, bool> PorTipo(ControlType t) => e => { try { return e.ControlType == t; } catch { return false; } };

IEnumerable<AutomationElement> Andar(AutomationElement? raiz, int maxNos = 6000)
{
    if (raiz is null) yield break;
    var w = uia.TreeWalkerFactory.GetRawViewWalker();
    var vistos = new HashSet<string>();
    int n = 0;
    // Busca em profundidade, na ordem do documento (filhos empilhados ao contrário).
    var pilha = new Stack<AutomationElement>(Enumerable.Reverse(Filhos(w, raiz)));
    while (pilha.Count > 0 && n++ < maxNos)
    {
        var e = pilha.Pop();
        string rid = Seg(() => string.Join(".", ((Interop.UIAutomationClient.IUIAutomationElement)((FlaUI.UIA3.UIA3FrameworkAutomationElement)e.FrameworkAutomationElement).NativeElement).GetRuntimeId() as int[] ?? []));
        if (rid is not ("" or "?") && !vistos.Add(rid)) continue;
        if (Environment.GetEnvironmentVariable("PROVA_DEPURAR") == "1") Console.Error.WriteLine($"{rid} {Seg(() => e.ControlType.ToString())} {Seg(() => e.AutomationId)}");
        yield return e;
        foreach (var f in Enumerable.Reverse(Filhos(w, e))) pilha.Push(f);
    }
}

List<AutomationElement> Filhos(FlaUI.Core.ITreeWalker w, AutomationElement e)
{
    var l = new List<AutomationElement>();
    try { for (var f = w.GetFirstChild(e); f != null; f = w.GetNextSibling(f)) l.Add(f); } catch { }
    return l;
}

AutomationElement? Primeiro(AutomationElement? raiz, Func<AutomationElement, bool> p) => Andar(raiz).FirstOrDefault(p);
AutomationElement[] Todos(AutomationElement? raiz, Func<AutomationElement, bool> p) => Andar(raiz).Where(p).ToArray();

string Titulo(AutomationElement item)
{
    // O nome da conversa é o primeiro bloco de texto do item. A busca realça o trecho achado partindo o texto em
    // vários nós ("Mat" + "eus") e emojis viram Image, então junta os irmãos do primeiro Text.
    var primeiro = Primeiro(item, PorTipo(ControlType.Text));
    if (primeiro is null) return "";
    // Sobe até a célula do título (o filho direto do DataItem "nome hora") e junta todos os textos e emojis dela: a busca
    // embrulha cada palavra achada num span, então os pedaços não são irmãos.
    var w = uia.TreeWalkerFactory.GetRawViewWalker();
    var celula = primeiro;
    for (int i = 0; i < 6; i++)
    {
        var pai = w.GetParent(celula);
        if (pai is null || PorTipo(ControlType.DataItem)(pai)) break;
        celula = pai;
    }
    var partes = new[] { celula }.Concat(Andar(celula, 200))
        .Where(e => PorTipo(ControlType.Text)(e) || PorTipo(ControlType.Image)(e))
        .Select(e => Seg(() => e.Name));
    return string.Concat(partes).Trim();
}

bool CabecalhoConfere(AutomationElement main, string nome)
{
    // O primeiro texto do painel da conversa, na ordem do documento, é o nome no cabeçalho (há grupos e contatos).
    // Emojis no cabeçalho são imagens à parte; compara sem eles.
    var t = Primeiro(main, PorTipo(ControlType.Text));
    static string SemEmoji(string s) => new string(s.Where(c => !char.IsSurrogate(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.OtherSymbol && c != '️').ToArray()).Trim();
    return t != null && SemEmoji(Seg(() => t.Name)) == SemEmoji(nome);
}

bool Devolver(nint hwnd, string estadoAntes, nint focoAntes, bool frente)
{
    if (estadoAntes == "minimizada") ShowWindow(hwnd, SW_SHOWMINNOACTIVE);
    else if (estadoAntes == "oculta") ShowWindow(hwnd, SW_HIDE);
    if (focoAntes != 0 && GetForegroundWindow() != focoAntes) SetForegroundWindow(focoAntes);
    Thread.Sleep(100);
    return GetForegroundWindow() == focoAntes;
}

string Estado(nint h) => !IsWindowVisible(h) ? "oculta" : IsIconic(h) ? "minimizada" : GetForegroundWindow() == h ? "frente" : "fundo";

T Esperar<T>(Func<T> f, int ms)
{
    var sw = Stopwatch.StartNew();
    while (true)
    {
        try { return f(); }
        catch (Exception) when (sw.ElapsedMilliseconds < ms) { Thread.Sleep(150); }
    }
}

List<(nint, string)> JanelasFilhas(nint pai)
{
    var l = new List<(nint, string)>();
    EnumChildWindows(pai, (h, _) => { l.Add((h, Classe(h))); return true; }, 0);
    return l;
}

static string Classe(nint h) { var sb = new System.Text.StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }

[DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid, nint outer, uint ctx, ref Guid iid, out nint ptr);
[DllImport("user32.dll")] static extern bool ShowWindow(nint h, int cmd);
[DllImport("user32.dll")] static extern bool SetForegroundWindow(nint h);
[DllImport("user32.dll")] static extern nint GetForegroundWindow();
[DllImport("user32.dll")] static extern bool IsIconic(nint h);
[DllImport("user32.dll")] static extern bool IsWindowVisible(nint h);
[DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, nint l);
[DllImport("user32.dll")] static extern bool EnumChildWindows(nint pai, EnumProc f, nint l);
[DllImport("user32.dll")] static extern bool PostMessage(nint h, uint msg, nint w, nint l);
[DllImport("user32.dll")] static extern bool ScreenToClient(nint h, ref PONTO p);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint h, out uint pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint h, System.Text.StringBuilder sb, int n);

partial class Program
{
    const int SW_HIDE = 0, SW_SHOWMINNOACTIVE = 7, SW_SHOWNOACTIVATE = 4, SW_RESTORE = 9;
    delegate bool EnumProc(nint h, nint l);
}

struct PONTO { public int x, y; }

[ComImport, Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface INotificationActivationCallback
{
    void Activate([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
                  [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] NOTIFICATION_USER_INPUT_DATA[] data, uint count);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct NOTIFICATION_USER_INPUT_DATA
{
    [MarshalAs(UnmanagedType.LPWStr)] public string Key;
    [MarshalAs(UnmanagedType.LPWStr)] public string Value;
}

record Resultado(bool Ok, string Etapa, long Ms, string EstadoAntes, bool FocoDevolvido, string EstadoDepois, string Detalhe)
{
    public override string ToString() => $"{(Ok ? "OK" : "FALHOU")} etapa={Etapa} {Ms} ms antes={EstadoAntes} depois={EstadoDepois} foco_devolvido={FocoDevolvido} {Detalhe}";
}

class Falha(string etapa, string msg) : Exception(msg) { public string Etapa { get; } = etapa; }
