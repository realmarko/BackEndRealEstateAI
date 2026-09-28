using RealEstate.Api.Extensions;
using Xunit;

namespace RealEstate.Api.Tests.Extensions;

public class GeoValidationTests
{
    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(19.0413)]
    public void IsValidLat_AcceptsValuesWithinRange(double lat)
    {
        Assert.True(GeoValidation.IsValidLat(lat));
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValidLat_RejectsOutOfRangeOrNonFiniteValues(double lat)
    {
        Assert.False(GeoValidation.IsValidLat(lat));
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    [InlineData(-98.2062)]
    public void IsValidLng_AcceptsValuesWithinRange(double lng)
    {
        Assert.True(GeoValidation.IsValidLng(lng));
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValidLng_RejectsOutOfRangeOrNonFiniteValues(double lng)
    {
        Assert.False(GeoValidation.IsValidLng(lng));
    }

    [Fact]
    public void ValidateLatLng_ReturnsNullForValidCoordinates()
    {
        var result = GeoValidation.ValidateLatLng(19.0413, -98.2062);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateLatLng_ReturnsLatMessageWhenLatIsInvalid()
    {
        var result = GeoValidation.ValidateLatLng(double.NaN, -98.2062);

        Assert.Equal("lat must be a finite number between -90 and 90.", result);
    }

    [Fact]
    public void ValidateLatLng_ReturnsLngMessageWhenLatIsValidButLngIsInvalid()
    {
        var result = GeoValidation.ValidateLatLng(19.0413, 200);

        Assert.Equal("lng must be a finite number between -180 and 180.", result);
    }

    [Fact]
    public void ValidateLatLng_ChecksLatBeforeLngWhenBothAreInvalid()
    {
        var result = GeoValidation.ValidateLatLng(200, 200);

        Assert.Equal("lat must be a finite number between -90 and 90.", result);
    }
}
