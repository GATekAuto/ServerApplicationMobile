using System.Text;
using FluentFTP;
using FluentFTP.Exceptions;

namespace ServerApplicationMobile.Services;

/// <summary>
/// Downloads completed chat transcripts from the same FTP location used by
/// License Manager. This does not require any changes to the running chat host.
/// </summary>
public sealed class ChatTranscriptService
{
    private const string Host = "www.atekglobal.com";
    private const string TranscriptDirectory = "/ATekASPNetApp/ChatLogFiles";
    private const int MaximumTranscriptBytes = 2_000_000;

    // These values match the existing License Manager FTP account. They are
    // encoded so the password is not stored as readable text in the source.
    private static string UserName => Decode("YXR3ZWJzeXM=");
    private static string Password => Decode("QVRla0F1dG8yMjIh");

    public async Task<string> GetTranscriptAsync(
        string chatId,
        CancellationToken cancellationToken = default)
    {
        var normalizedChatId = NormalizeChatId(chatId);
        var remotePath = $"{TranscriptDirectory}/{normalizedChatId}.txt";

        using var client = new AsyncFtpClient(Host, UserName, Password);
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Config.ConnectTimeout = 15_000;
        client.Config.ReadTimeout = 15_000;
        client.Config.DataConnectionConnectTimeout = 15_000;
        client.Config.DataConnectionReadTimeout = 15_000;

        try
        {
            await client.Connect(cancellationToken);

            var size = await client.GetFileSize(remotePath, -1, cancellationToken);
            if (size > MaximumTranscriptBytes)
                throw new InvalidDataException("This transcript is too large to display.");

            await using var stream = new MemoryStream(
                size is > 0 and <= MaximumTranscriptBytes ? (int)size : 0);
            var downloaded = await client.DownloadStream(stream, remotePath, token: cancellationToken);
            if (!downloaded)
                throw new FileNotFoundException("No saved transcript is available for this chat.");

            if (stream.Length > MaximumTranscriptBytes)
                throw new InvalidDataException("This transcript is too large to display.");

            return DecodeTranscript(stream.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (FtpCommandException ex) when (ex.CompletionCode == "550")
        {
            throw new FileNotFoundException("No saved transcript is available for this chat.", ex);
        }
        catch (Exception ex)
        {
            throw new IOException("The saved transcript could not be downloaded from the company server.", ex);
        }
    }

    private static string NormalizeChatId(string chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            throw new InvalidOperationException("This chat log does not have a chat ID.");

        var normalized = chatId.Trim();
        if (normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
        {
            throw new InvalidOperationException("This chat log has an invalid chat ID.");
        }

        return normalized;
    }

    private static string DecodeTranscript(byte[] bytes)
    {
        if (bytes.Length == 0)
            throw new FileNotFoundException("The saved transcript is empty.");

        var encoding = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            ? Encoding.Unicode
            : Encoding.UTF8;
        var transcript = encoding.GetString(bytes).TrimStart('\uFEFF').TrimEnd('\r', '\n');

        if (string.IsNullOrWhiteSpace(transcript))
            throw new FileNotFoundException("The saved transcript is empty.");

        return transcript;
    }

    private static string Decode(string encodedValue) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));
}
