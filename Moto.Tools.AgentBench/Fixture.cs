// Moto.Tools.AgentBench/Fixture.cs
// Le mini-projet « Shop » sur lequel l'agent travaille : petit mais réaliste (plusieurs fichiers, une
// duplication à factoriser, une méthode appelée depuis 3 fichiers, un fichier de ~430 lignes). Généré dans un dossier
// jetable pour que chaque essai reparte du même état.
using System.Text;

namespace Moto.Tools.AgentBench;

internal static class Fixture
{
    public const string ProjectFile = "Shop.csproj";

    /// <summary>Nombre attendu de méthodes publiques d'InventoryService (tâche « question »).</summary>
    public const int InventoryPublicMethods = 9;

    public static IReadOnlyDictionary<string, string> Files()
    {
        var files = new Dictionary<string, string>
        {
            [ProjectFile] = Csproj,
            ["Program.cs"] = Program,
            ["Models/Product.cs"] = Product,
            ["Services/InventoryService.cs"] = Inventory,
            ["Services/PricingService.cs"] = Pricing,
            ["Services/InvoiceService.cs"] = Invoice,
            ["Services/IReportWriter.cs"] = ReportWriter,
            ["Services/Big/LegacyCatalog.cs"] = LegacyCatalog(),
        };
        return files;
    }

    public static void WriteTo(string root)
    {
        foreach (var (rel, content) in Files())
        {
            var full = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
        }
    }

    private const string Csproj = """
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
""";

    private const string Program = """
using System.Globalization;
using Shop.Models;
using Shop.Services;
using Shop.Services.Big;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var inventory = new InventoryService();
inventory.AddProduct(new Product(1, "Clavier", 49.90m, 12));
inventory.AddProduct(new Product(2, "Souris", 19.90m, 0));
inventory.AddProduct(new Product(3, "Ecran", 189.00m, 4));
inventory.Restock(2, 10);
inventory.Consume(1, 2);

var pricing = new PricingService();
var invoices = new InvoiceService(pricing);

Console.WriteLine($"Valeur du stock : {inventory.TotalValue():F2}");
Console.WriteLine($"Clavier TTC : {pricing.PriceWithTax(49.90m):F2}");
Console.WriteLine($"Taxe sur 100 : {pricing.ComputeTax(100m):F2}");
Console.WriteLine(invoices.BuildInvoice(inventory.GetById(1)!, 2));
Console.WriteLine($"Stock bas : {string.Join(", ", inventory.LowStock(5).Select(p => p.Name))}");
Console.WriteLine($"Catalogue : {new LegacyCatalog().ComputePriceBand7(100m):F2}");
""";

    private const string Product = """
namespace Shop.Models;

public class Product
{
    public int Id { get; }
    public string Name { get; }
    public decimal Price { get; }
    public int Stock { get; set; }

    public Product(int id, string name, decimal price, int stock)
    {
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
    }
}
""";

    public const string InventoryOriginalTotalValueLine = "        return _products.Values.Sum(p => p.Price * p.Stock);";

    private const string Inventory = """
using Shop.Models;

namespace Shop.Services;

public class InventoryService
{
    private readonly Dictionary<int, Product> _products = new();

    public void AddProduct(Product product)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));
        if (_products.ContainsKey(product.Id))
            throw new InvalidOperationException($"Produit {product.Id} déjà présent.");
        _products[product.Id] = product;
    }

    public Product? FindByName(string name)
    {
        return _products.Values.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public Product? GetById(int id)
    {
        return _products.TryGetValue(id, out var product) ? product : null;
    }

    public void Restock(int id, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La quantité doit être positive.");
        if (quantity > 1000)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La quantité est trop grande.");

        var product = GetById(id) ?? throw new KeyNotFoundException($"Produit {id} inconnu.");
        product.Stock += quantity;
    }

    public void Consume(int id, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La quantité doit être positive.");
        if (quantity > 1000)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La quantité est trop grande.");

        var product = GetById(id) ?? throw new KeyNotFoundException($"Produit {id} inconnu.");
        if (product.Stock < quantity)
            throw new InvalidOperationException($"Stock insuffisant pour {product.Name}.");
        product.Stock -= quantity;
    }

    public bool Remove(int id)
    {
        return _products.Remove(id);
    }

    public decimal TotalValue()
    {
        return _products.Values.Sum(p => p.Price * p.Stock);
    }

    public IReadOnlyList<Product> LowStock(int threshold)
    {
        return _products.Values.Where(p => p.Stock <= threshold).OrderBy(p => p.Stock).ToList();
    }

    public IReadOnlyList<Product> All()
    {
        return _products.Values.OrderBy(p => p.Id).ToList();
    }
}
""";

    private const string Pricing = """
namespace Shop.Services;

public class PricingService
{
    private const decimal TaxRate = 0.20m;

    public decimal ApplyDiscount(decimal price, decimal percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent));
        return Round(price - price * percent / 100m);
    }

    public decimal ComputeTax(decimal amount)
    {
        return Round(amount * TaxRate);
    }

    public decimal PriceWithTax(decimal price)
    {
        return Round(price + ComputeTax(price));
    }

    public decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
""";

    private const string Invoice = """
using Shop.Models;

namespace Shop.Services;

public class InvoiceService
{
    private readonly PricingService _pricing;

    public InvoiceService(PricingService pricing)
    {
        _pricing = pricing;
    }

    public string BuildInvoice(Product product, int quantity)
    {
        var subtotal = product.Price * quantity;
        var tax = _pricing.ComputeTax(subtotal);
        var total = subtotal + tax;
        return $"Facture : {quantity} x {product.Name} = {subtotal:F2} + taxe {tax:F2} = {total:F2}";
    }
}
""";

    private const string ReportWriter = """
namespace Shop.Services;

public interface IReportWriter
{
    string Title { get; }

    void WriteHeader(string title);

    void WriteLine(string text);

    void WriteFooter(int lineCount);
}
""";

    /// <summary>Un long fichier (~430 lignes) : 42 méthodes très semblables, et une constante MaxItems au milieu.</summary>
    private static string LegacyCatalog()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Shop.Services.Big;");
        sb.AppendLine();
        sb.AppendLine("// Ancien catalogue : nombreuses tranches de prix héritées, à ne pas toucher sans raison.");
        sb.AppendLine("public class LegacyCatalog");
        sb.AppendLine("{");

        for (var n = 1; n <= 42; n++)
        {
            if (n == 22)
            {
                sb.AppendLine("    public const int MaxItems = 100;");
                sb.AppendLine();
                sb.AppendLine("    public bool IsFull(int count)");
                sb.AppendLine("    {");
                sb.AppendLine("        return count >= MaxItems;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine($"    public decimal ComputePriceBand{n}(decimal basePrice)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var band = basePrice * {n} / 100m;");
            sb.AppendLine("        if (band < 1m)");
            sb.AppendLine("            band = 1m;");
            sb.AppendLine("        return Math.Round(basePrice + band, 2);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
