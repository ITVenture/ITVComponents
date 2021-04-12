using ITVComponents.DataExchange.Import;

namespace ITVComponents.DataExchange.TextImport
{
    public interface ITextSource :IImportSource<string, TextAcceptanceCallbackParameter>
    {
    }
}
