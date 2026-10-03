using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class PipelineStageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool RequiresLegalReview { get; set; }
}

public class SaleProcessCreateDto
{
    [Required, MaxLength(200)] public string ClientName { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string ClientPhone { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string PropertyAddress { get; set; } = string.Empty;
    [Required] public decimal EstimatedPrice { get; set; }
    public int? StageId { get; set; }
    public Guid? ListingId { get; set; }
}

public class SaleProcessStageUpdateDto
{
    [Required] public int StageId { get; set; }
}

public class SaleProcessDocumentUpdateDto
{
    [Required] public bool IsVerified { get; set; }
}

public class SaleProcessTaskCreateDto
{
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
}

public class SaleProcessTaskUpdateDto
{
    [Required] public bool IsCompleted { get; set; }
}

// Summary shape for the board's cards/list — counts only, no document/task contents.
public class SaleProcessDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public decimal EstimatedPrice { get; set; }
    public Guid? ListingId { get; set; }

    public int CurrentStageId { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public bool RequiresLegalReview { get; set; }

    public int DocumentsTotal { get; set; }
    public int DocumentsVerified { get; set; }
    public int TasksTotal { get; set; }
    public int TasksCompleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaleProcessDocumentDto
{
    public Guid Id { get; set; }
    public int StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public class SaleProcessTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Full shape for the deal-detail panel: the summary plus every checklist item and task.
public class SaleProcessDetailDto : SaleProcessDto
{
    public List<SaleProcessDocumentDto> Documents { get; set; } = new();
    public List<SaleProcessTaskDto> Tasks { get; set; } = new();
}

public class PipelineMetricsDto
{
    public int ActiveCount { get; set; }
    public decimal TotalValue { get; set; }
    public int LegalReviewCount { get; set; }
    public int ClosedCount { get; set; }
}
