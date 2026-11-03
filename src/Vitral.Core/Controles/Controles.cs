namespace Vitral.Core.Controles;

/// O tipo de um aparelho Bluetooth, para o ícone e para o clique (spec dos controles §3).
public enum TipoAparelho { Audio, Teclado, Mouse, Celular, Controle, Outro }

/// Um aparelho Bluetooth pareado. <see cref="TemAudio"/>: tem saída de áudio Bluetooth (o clique conecta e desconecta).
public sealed record AparelhoBluetooth(string Id, string Nome, TipoAparelho Tipo, bool Conectado, bool TemAudio);
