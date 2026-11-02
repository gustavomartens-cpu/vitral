using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vitral.Core.Perfis;

/// Campo de enum anulável com um valor que esta versão não conhece (uma opção nova de uma versão futura): vira nulo,
/// "Não mexer", e o resto do perfil é lido. Assim uma opção nova não exige subir o esquema. Enum obrigatório continua
/// recusando, porque não tem "Não mexer" para cair.
internal sealed class EnumTolerante : JsonConverterFactory
{
    private readonly JsonStringEnumConverter _texto = new(JsonNamingPolicy.CamelCase, allowIntegerValues: false);

    public override bool CanConvert(Type tipo) => Nullable.GetUnderlyingType(tipo) is { IsEnum: true };

    public override JsonConverter CreateConverter(Type tipo, JsonSerializerOptions opcoes)
    {
        var enumTipo = Nullable.GetUnderlyingType(tipo)!;
        var interno = _texto.CreateConverter(enumTipo, opcoes);
        return (JsonConverter)Activator.CreateInstance(typeof(Conversor<>).MakeGenericType(enumTipo), interno)!;
    }

    private sealed class Conversor<T>(JsonConverter interno) : JsonConverter<T?> where T : struct, Enum
    {
        private readonly JsonConverter<T> _interno = (JsonConverter<T>)interno;

        public override bool HandleNull => true;

        public override T? Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
        {
            if (leitor.TokenType == JsonTokenType.Null) return null;
            if (leitor.TokenType != JsonTokenType.String) return _interno.Read(ref leitor, typeof(T), opcoes);
            try { return _interno.Read(ref leitor, typeof(T), opcoes); }
            catch (JsonException) { return null; }
        }

        public override void Write(Utf8JsonWriter escritor, T? valor, JsonSerializerOptions opcoes)
        {
            if (valor is { } v) _interno.Write(escritor, v, opcoes);
            else escritor.WriteNullValue();
        }
    }
}
