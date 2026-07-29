using ConAuto.SharedEnums;

namespace ServerApplicationMobile.Services;

public sealed class ServiceTicket
{
    public string TicketNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string OEM { get; init; } = string.Empty;
    public DateTime CallDate { get; init; }
    public enumATekServiceCallProblemType ProblemType { get; init; }
    public string TicketCreatedBy { get; init; } = string.Empty;
    public string JobNumber { get; init; } = string.Empty;
    public string ProblemInfo { get; init; } = string.Empty;
    public string ProblemSolution { get; init; } = string.Empty;
    public string TroubleShootingSteps { get; init; } = string.Empty;
    public bool IsClosed { get; init; }
    public DateTime? TicketClosedDate { get; init; }
    public string TicketClosedBy { get; init; } = string.Empty;
    public bool IsNeedToSendTech { get; init; }
    public string Remarks { get; init; } = string.Empty;
    public string SoftwareType { get; init; } = string.Empty;
    public string SoftwareVersion { get; init; } = string.Empty;
    public string SoftwareMinorVersion { get; init; } = string.Empty;
    public string MachineArea { get; init; } = string.Empty;
    public string MachineItem { get; init; } = string.Empty;

    public string Status => IsClosed ? "Closed" : "Open";
    public string DisplayDate => CallDate == default ? string.Empty : CallDate.ToString("d");
    public string Details => string.Join(" | ", new[] { JobNumber, OEM, ProblemType.ToString() }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
    public string VersionDisplay => string.Join('.', new[] { SoftwareVersion, SoftwareMinorVersion }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class SoftwareLog
{
    public long ID { get; init; }
    public DateTime LogDate { get; init; }
    public int MajorVersion { get; init; }
    public int MinorVersion { get; init; }
    public int Build { get; init; }
    public string SoftwareType { get; init; } = string.Empty;
    public enumSoftwareLogType LogType { get; init; }
    public string Description { get; init; } = string.Empty;
    public string InternalRemarks { get; init; } = string.Empty;
    public string LogBy { get; init; } = string.Empty;
    public bool IsHidden { get; init; }

    public string Version => $"{MajorVersion}.{MinorVersion}.{Build}";
    public string TypeDisplay => LogType switch
    {
        enumSoftwareLogType.BugFixed => "Bug Fixed",
        enumSoftwareLogType.NewFeature => "New Feature",
        enumSoftwareLogType.ReportBug => "Report Bug",
        _ => LogType.ToString()
    };
    public string DisplayDate => LogDate == default ? string.Empty : LogDate.ToString("d");
}

public sealed class ChatLog
{
    public long ID { get; init; }
    public DateTime? LogDate { get; init; }
    public string JobNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string OEMName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Message1 { get; init; } = string.Empty;
    public string Message2 { get; init; } = string.Empty;
    public string ChatID { get; init; } = string.Empty;
    public DateTime? StartTime { get; init; }
    public DateTime? AcceptedTime { get; init; }

    public string DisplayDate => LogDate?.ToString("g") ?? string.Empty;
    public string Message => string.Concat(Message1, Message2).Trim();
    public string MessagePreview => string.IsNullOrWhiteSpace(Message) ? "No message recorded." : Message;
    public string Details => string.Join(" | ", new[] { JobNumber, OEMName, UserName }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
}
