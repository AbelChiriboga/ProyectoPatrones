using System.Text.Json;
using Pos.Application.Sales;

namespace Pos.Infrastructure.Sales;

/// <summary>Single-host durable journal. Never delete claims, including incomplete crash remnants.</summary>
public sealed class FileSaleOperationStore(string directory) : ISaleOperationStore
{
    private string FilePath(Guid id, string suffix) => Path.Combine(directory, id.ToString("D") + suffix);
    public bool TryCreate(SaleOperation operation) {
        Directory.CreateDirectory(directory);
        FileStream file;
        try { file = new FileStream(FilePath(operation.Id, ".claim"), FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (IOException) when (File.Exists(FilePath(operation.Id, ".claim"))) { return false; }
        using (file) { JsonSerializer.Serialize(file, operation); file.Flush(true); }
        return true;
    }
    public SaleOperation? Read(Guid id) {
        var path = FilePath(id, ".claim");
        if (!File.Exists(path)) return null;
        try { using var file = File.OpenRead(path); return JsonSerializer.Deserialize<SaleOperation>(file) ?? throw new IOException(); }
        catch (Exception e) when (e is IOException or JsonException) {
            throw new SaleConflictException("Operación en curso o diario incompleto; no reenviar. Requiere conciliación.");
        }
    }
    public OperationResult? Result(Guid id) {
        var path = FilePath(id, ".result");
        return File.Exists(path) ? JsonSerializer.Deserialize<OperationResult>(File.ReadAllText(path)) : null;
    }
    public void Confirm(OperationResult result) {
        var temp = FilePath(result.OperationId, "." + Guid.NewGuid() + ".tmp");
        using (var file = new FileStream(temp, FileMode.CreateNew)) { JsonSerializer.Serialize(file, result); file.Flush(true); }
        var destination = FilePath(result.OperationId, ".result");
        try { File.Move(temp, destination, false); }
        catch (IOException) when (File.Exists(destination)) { File.Delete(temp); }
        catch (UnauthorizedAccessException) when (File.Exists(destination)) { File.Delete(temp); }
    }
}
