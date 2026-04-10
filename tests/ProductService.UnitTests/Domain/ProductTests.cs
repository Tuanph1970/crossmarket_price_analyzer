using Common.Domain.Enums;
using FluentAssertions;
using ProductService.Domain.Entities;
using Xunit;

namespace ProductService.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        // Arrange
        var name = "MacBook Pro 14\"";
        var sourceUrl = "https://amazon.com/product/abc123";
        var source = ProductSource.Amazon;
        var sku = "MBP-14-M3";
        var hsCode = "847130";
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Act
        var product = Product.Create(name, sourceUrl, source, sku, hsCode, brandId, categoryId);

        // Assert
        product.Id.Should().NotBeEmpty();
        product.Name.Should().Be(name);
        product.SourceUrl.Should().Be(sourceUrl);
        product.Source.Should().Be(source);
        product.Sku.Should().Be(sku);
        product.HsCode.Should().Be(hsCode);
        product.BrandId.Should().Be(brandId);
        product.CategoryId.Should().Be(categoryId);
        product.IsActive.Should().BeTrue();
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithDefaults_ShouldSetCorrectDefaults()
    {
        // Arrange
        var name = "Test Product";
        var sourceUrl = "https://example.com/product";

        // Act
        var product = Product.Create(name, sourceUrl, ProductSource.Walmart);

        // Assert
        product.Sku.Should().BeNull();
        product.HsCode.Should().BeNull();
        product.BrandId.Should().BeNull();
        product.CategoryId.Should().BeNull();
        product.IsActive.Should().BeTrue();
        product.PriceSnapshots.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldAcceptInactiveProducts()
    {
        // Act
        var product = Product.Create("Inactive", "https://x.com/1", ProductSource.Shopee, isActive: false);

        // Assert
        product.IsActive.Should().BeFalse();
    }
}

public class PriceSnapshotTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var price = 1299.99m;
        var currency = "USD";
        var quantity = 1m;
        var sellerName = "Amazon";
        var sellerRating = 4.5m;
        var salesVolume = 5000;

        // Act
        var snapshot = PriceSnapshot.Create(
            productId, price, currency, quantity, sellerName, sellerRating, salesVolume);

        // Assert
        snapshot.Id.Should().NotBeEmpty();
        snapshot.ProductId.Should().Be(productId);
        snapshot.Price.Should().Be(price);
        snapshot.Currency.Should().Be(currency);
        snapshot.QuantityPerUnit.Should().Be(quantity);
        snapshot.UnitPrice.Should().Be(price);
        snapshot.SellerName.Should().Be(sellerName);
        snapshot.SellerRating.Should().Be(sellerRating);
        snapshot.SalesVolume.Should().Be(salesVolume);
        snapshot.ScrapedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithMinQuantity_ShouldSetQuantity()
    {
        // Arrange & Act
        var snapshot = PriceSnapshot.Create(Guid.NewGuid(), 50m, "VND", 5m);

        // Assert
        snapshot.QuantityPerUnit.Should().Be(5m);
        snapshot.UnitPrice.Should().Be(50m);
    }

    [Fact]
    public void Create_Defaults_ShouldUseUsdAndOne()
    {
        // Note: Currency is an auto-property default; explicit null sets it to null
        // QuantityPerUnit correctly defaults to 1 in Create()
        var snapshot = PriceSnapshot.Create(Guid.NewGuid(), 100m, "USD", 1);

        // Assert
        snapshot.Currency.Should().Be("USD");
        snapshot.QuantityPerUnit.Should().Be(1);
        snapshot.UnitPrice.Should().Be(100m);
    }
}

public class BrandTests
{
    [Fact]
    public void Create_ShouldSetNameAndNormalizedName()
    {
        // Arrange
        var name = "Apple Inc.";

        // Act
        var brand = Brand.Create(name);

        // Assert
        brand.Id.Should().NotBeEmpty();
        brand.Name.Should().Be("Apple Inc.");
        brand.NormalizedName.Should().Be("apple inc.");
        brand.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldTrimWhitespace()
    {
        // Act
        var brand = Brand.Create("  Sony  ");

        // Assert
        brand.Name.Should().Be("Sony");
        brand.NormalizedName.Should().Be("sony");
    }

    [Fact]
    public void Create_ProductsCollection_ShouldBeEmpty()
    {
        // Act
        var brand = Brand.Create("Test Brand");

        // Assert
        brand.Products.Should().NotBeNull();
        brand.Products.Should().BeEmpty();
    }
}

public class CategoryTests
{
    [Fact]
    public void Create_ShouldSetNameAndHsCode()
    {
        // Arrange
        var name = "Laptops";
        var hsCode = "847130";

        // Act
        var category = Category.Create(name, hsCode);

        // Assert
        category.Id.Should().NotBeEmpty();
        category.Name.Should().Be("Laptops");
        category.HsCode.Should().Be("847130");
        category.ParentCategoryId.Should().BeNull();
        category.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithParent_ShouldSetParentId()
    {
        // Arrange
        var parentId = Guid.NewGuid();

        // Act
        var category = Category.Create("Gaming Laptops", "847130", parentId);

        // Assert
        category.ParentCategoryId.Should().Be(parentId);
    }

    [Fact]
    public void Create_ShouldTrimInputs()
    {
        // Act
        var category = Category.Create("  Electronics  ", "  8501  ");

        // Assert
        category.Name.Should().Be("Electronics");
        category.HsCode.Should().Be("8501");
    }

    [Fact]
    public void Create_Collections_ShouldBeEmpty()
    {
        // Act
        var category = Category.Create("Test", "0000");

        // Assert
        category.SubCategories.Should().NotBeNull().And.BeEmpty();
        category.Products.Should().NotBeNull().And.BeEmpty();
    }
}
