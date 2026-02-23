using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class DeterministicTransactionEmbeddingGenerator : ITransactionEmbeddingGenerator
{
    public const int EmbeddingDimensions = 1536;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = EmbeddingTextHasher.Normalize(text);
        if (normalized.Length == 0)
        {
            return Task.FromResult(new float[EmbeddingDimensions]);
        }

        var seed = Encoding.UTF8.GetBytes(normalized);
        var values = new float[EmbeddingDimensions];
        var valueIndex = 0;
        var blockIndex = 0;

        while (valueIndex < values.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = new byte[seed.Length + sizeof(int)];
            Buffer.BlockCopy(seed, 0, payload, 0, seed.Length);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(seed.Length), blockIndex++);

            var digest = SHA256.HashData(payload);
            for (var i = 0; i < digest.Length && valueIndex < values.Length; i += 2)
            {
                if (i + 1 >= digest.Length)
                {
                    break;
                }

                var raw = BinaryPrimitives.ReadUInt16LittleEndian(digest.AsSpan(i, 2));
                values[valueIndex++] = (raw / 32767.5f) - 1f;
            }
        }

        NormalizeL2(values);
        return Task.FromResult(values);
    }

    private static void NormalizeL2(float[] values)
    {
        double sumOfSquares = 0;
        for (var i = 0; i < values.Length; i++)
        {
            sumOfSquares += values[i] * values[i];
        }

        if (sumOfSquares <= double.Epsilon)
        {
            return;
        }

        var length = (float)Math.Sqrt(sumOfSquares);
        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= length;
        }
    }
}
