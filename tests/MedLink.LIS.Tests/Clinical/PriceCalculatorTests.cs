// FR-GAP-060: розрахунок вартості — каса, покриття страховою, часткове покриття, франшиза, непокриті позиції, вибір пацієнта, пакети
using MedLink.LIS.Core.Billing;

namespace MedLink.LIS.Tests.Clinical;

public class PriceCalculatorTests
{
    private static readonly RetailTerms Retail = new()
    {
        PatientPayerId = "patient", PriceListId = "retail",
        Prices = new Dictionary<string, decimal> { ["CBC"] = 250, ["BIO"] = 550, ["TSH"] = 200, ["FT4"] = 220, ["VITD"] = 450 },
        Packages = new[] { new PricePackageDef { Id = "pkg-thyr", Code = "PKG-THYR", Name = "Щитоподібна залоза", Price = 360, MemberIds = new[] { "TSH", "FT4" }, PriceListId = "retail" } }
    };

    private static BillableItem I(string id, bool choice = false) => new() { Id = id, Code = id, Name = id, IsProfile = id is "CBC" or "BIO", PatientChoice = choice };

    [Fact]
    public void Patient_pays_retail_and_retail_package_applies()
    {
        var q = PriceCalculator.Calculate(new[] { I("CBC"), I("TSH"), I("FT4") }, new PayerTerms { PayerId = "patient", Kind = "PATIENT" }, Retail);
        Assert.Equal(2, q.Lines.Count);
        Assert.Equal(250 + 360, q.Total);
        Assert.Equal(q.Total, q.PatientTotal);
        Assert.Contains(q.Lines, l => l.PackageId == "pkg-thyr" && l.PackageMembers.Count == 2);
    }

    [Fact]
    public void Insurance_covers_listed_items_and_patient_pays_the_rest()
    {
        var insurer = new PayerTerms
        {
            PayerId = "uniqa", Kind = "INSURANCE", PriceListId = "uniqa-pl", CoveragePct = 80, FranchiseAmount = 0,
            Prices = new Dictionary<string, decimal> { ["CBC"] = 230, ["BIO"] = 500 }
        };
        var q = PriceCalculator.Calculate(new[] { I("CBC"), I("BIO"), I("VITD") }, insurer, Retail);
        var cbc = q.Lines.Single(l => l.Code == "CBC");
        Assert.Equal(230, cbc.Amount);
        Assert.Equal(184, cbc.PayerAmount);
        Assert.Equal(46, cbc.PatientAmount);
        var vitd = q.Lines.Single(l => l.Code == "VITD");
        Assert.True(vitd.IsNotCovered);
        Assert.Equal("patient", vitd.PayerId);
        Assert.Equal(450, vitd.PatientAmount);
        Assert.Equal(230 + 500 + 450, q.Total);
        Assert.Equal(q.Total, q.PayerTotal + q.PatientTotal);
    }

    [Fact]
    public void Franchise_moves_first_amount_to_patient_and_patient_choice_is_respected()
    {
        var insurer = new PayerTerms
        {
            PayerId = "ins", Kind = "INSURANCE", CoveragePct = 100, FranchiseAmount = 100,
            Prices = new Dictionary<string, decimal> { ["CBC"] = 230, ["BIO"] = 500 }
        };
        var q = PriceCalculator.Calculate(new[] { I("CBC"), I("BIO", choice: true) }, insurer, Retail);
        Assert.Equal(100, q.FranchiseApplied);
        Assert.Equal(130, q.Lines.Single(l => l.Code == "CBC").PayerAmount);
        var bio = q.Lines.Single(l => l.Code == "BIO");
        Assert.True(bio.IsPatientChoice);
        Assert.Equal(550, bio.PatientAmount); // за роздрібним прайсом
        Assert.Equal(100 + 550, q.PatientTotal);
    }

    [Fact]
    public void Nszu_is_free_for_patient_and_payer_package_beats_retail()
    {
        var nszu = new PayerTerms
        {
            PayerId = "nszu", Kind = "NSZU", CoveragePct = 100, Prices = new Dictionary<string, decimal> { ["CBC"] = 0, ["BIO"] = 0 },
            Packages = new[] { new PricePackageDef { Id = "nszu-pkg", Code = "PMG-LAB", Name = "ПМГ: базові аналізи", Price = 0, MemberIds = new[] { "CBC", "BIO" } } }
        };
        var q = PriceCalculator.Calculate(new[] { I("CBC"), I("BIO"), I("TSH") }, nszu, Retail);
        Assert.Contains(q.Lines, l => l.PackageId == "nszu-pkg" && l.PatientAmount == 0);
        Assert.Equal(200, q.PatientTotal); // TSH поза програмою — каса
        Assert.Equal(0, q.PayerTotal);
    }
}
