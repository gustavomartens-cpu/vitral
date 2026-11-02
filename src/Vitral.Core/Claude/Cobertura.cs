namespace Vitral.Core.Claude;

/// Um retângulo de janela na tela, em pixels.
public readonly record struct Area(double X, double Y, double Largura, double Altura);

/// Se uma janela está à vista pelas janelas que estão acima dela (revisão final da 0.13.5): uma janelinha por cima não
/// esconde o Claude, e uma janela de outro monitor que avança sobre ele conta. À vista = pelo menos metade da área
/// descoberta. As sobreposições entre as de cima contam uma vez só.
public static class Cobertura
{
    public static bool AVista(Area janela, IReadOnlyList<Area> acima)
    {
        double total = janela.Largura * janela.Altura;
        if (total <= 0) return false;
        // Recortes dentro da janela; a área coberta é a da união, por faixas nas coordenadas dos recortes.
        var cortes = acima.Select(a => Recortar(a, janela)).Where(a => a.Largura > 0 && a.Altura > 0).ToList();
        var xs = cortes.SelectMany(a => new[] { a.X, a.X + a.Largura }).Distinct().Order().ToList();
        double coberta = 0;
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            double meio = (xs[i] + xs[i + 1]) / 2;
            var faixas = cortes.Where(a => a.X <= meio && meio < a.X + a.Largura).Select(a => (a.Y, Fim: a.Y + a.Altura)).OrderBy(f => f.Y);
            double altura = 0, ate = double.NegativeInfinity;
            foreach (var (y, fim) in faixas)
            {
                if (fim <= ate) continue;
                altura += fim - Math.Max(y, ate);
                ate = fim;
            }
            coberta += altura * (xs[i + 1] - xs[i]);
        }
        return coberta <= total / 2;
    }

    private static Area Recortar(Area a, Area b)
    {
        double x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        double dir = Math.Min(a.X + a.Largura, b.X + b.Largura), baixo = Math.Min(a.Y + a.Altura, b.Y + b.Altura);
        return new Area(x, y, Math.Max(0, dir - x), Math.Max(0, baixo - y));
    }
}
