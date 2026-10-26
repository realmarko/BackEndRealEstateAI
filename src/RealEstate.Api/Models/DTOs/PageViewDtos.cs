namespace RealEstate.Api.Models.DTOs;

public class PageViewRecordDto
{
    public string PathPattern { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class PagePathViewsDto
{
    public string PathPattern { get; set; } = string.Empty;
    public int Views { get; set; }
}

// One week's worth of page views, Pages ordered most-viewed first.
public class WeeklyPageViewsDto
{
    public DateTime WeekStart { get; set; }
    public List<PagePathViewsDto> Pages { get; set; } = new();
}
