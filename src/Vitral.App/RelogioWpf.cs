using System.Windows.Threading;
using Vitral.Ui;

namespace Vitral.App;

/// Relógio de 1 s na thread da interface.
internal sealed class RelogioWpf : IRelogio
{
    public IDisposable ACadaSegundo(Action tique)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => tique();
        timer.Start();
        return new Parar(timer);
    }

    private sealed class Parar(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
