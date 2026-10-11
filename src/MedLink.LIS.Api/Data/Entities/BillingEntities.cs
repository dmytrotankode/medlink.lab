// =============================================================================
// FR-GAP-060. Джерела оплати, прайс-листи, пакети, страхові програми й поліси, нарахування, оплати, рахунки.
// У MedLink немає рахунків/оплат/страхових полісів (є лише org_organization_service.price,
// mis_appointment.is_payment_needed, ehd_contract (НСЗУ), ehe_medical_service_program) — тому:
//   [ЛІС]        — прайси, платники, нарахування, оплати, рахунки лабораторії;
//   [MedLink-new] — mis_patient_insurance: страховий поліс пацієнта, загальна сутність МІС (не лише лабораторія),
//                   пропонується до ядра MedLink.
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

public static class PayerKinds
{
    /// <summary>Пацієнт (каса, роздрібний прайс).</summary>
    public const string Patient = "PATIENT";
    /// <summary>Страхова програма (ДМС).</summary>
    public const string Insurance = "INSURANCE";
    public const string Enterprise = "ENTERPRISE";
    /// <summary>Клініка-партнер, що направляє проби (FR-REF-002).</summary>
    public const string Clinic = "CLINIC";
    /// <summary>НСЗУ — програма медичних гарантій (ehe_medical_service_program); для пацієнта безоплатно.</summary>
    public const string Nszu = "NSZU";
    /// <summary>Внутрішнє (власні потреби закладу).</summary>
    public const string Internal = "INTERNAL";
    public static readonly string[] All = { Patient, Insurance, Enterprise, Clinic, Nszu, Internal };
    public static string Label(string k) => k switch
    {
        Patient => "Пацієнт (каса)", Insurance => "Страхова програма", Enterprise => "Підприємство", Clinic => "Клініка-партнер", Nszu => "НСЗУ (ПМГ)", Internal => "Внутрішнє", _ => k
    };
}

/// <summary>[ЛІС] lab_price_list — прайс-лист (роздрібний, страхової програми, договірний, НСЗУ, клініки-партнера).</summary>
[Table("lab_price_list")]
public class LabPriceList : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    /// <summary>Роздрібний прайс за замовчуванням (каса; позиції, не покриті прайсом платника).</summary>
    public bool IsDefault { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "UAH";
    public bool IsActive { get; set; } = true;

    public List<LabPriceListItem> Items { get; set; } = new();
    public List<LabPricePackage> Packages { get; set; } = new();
}

/// <summary>[ЛІС] lab_price_list_item — ціна послуги (профілю) або окремого показника. Наявність позиції = покриття платником.</summary>
[Table("lab_price_list_item")]
public class LabPriceListItem : GuidEntity
{
    [MaxLength(64)] public string PriceListId { get; set; } = "";
    [MaxLength(64)] public string? ProfileId { get; set; }
    [MaxLength(64)] public string? TestId { get; set; }
    public decimal Price { get; set; }

    [ForeignKey(nameof(PriceListId))] public LabPriceList? PriceList { get; set; }
}

/// <summary>[ЛІС] lab_price_package — пакет послуг за пакетною ціною (застосовується, коли в замовленні є всі складові).</summary>
[Table("lab_price_package")]
public class LabPricePackage : GuidEntity
{
    [MaxLength(64)] public string PriceListId { get; set; } = "";
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    public decimal Price { get; set; }
    /// <summary>Складові: ідентифікатори профілів і показників (JSON-масив рядків).</summary>
    public string MemberIdsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(PriceListId))] public LabPriceList? PriceList { get; set; }
    [NotMapped]
    public List<string> MemberIds
    {
        get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(MemberIdsJson) ?? new();
        set => MemberIdsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new());
    }
}

/// <summary>[ЛІС] lab_payer — платник / програма: страхова програма, підприємство, клініка, НСЗУ, пацієнт. Умови покриття — тут.</summary>
[Table("lab_payer")]
public class LabPayer : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    /// <summary>Назва програми/договору (напр. «УНІКА — Корпоративна Gold»).</summary>
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>PATIENT | INSURANCE | ENTERPRISE | CLINIC | NSZU | INTERNAL</summary>
    [MaxLength(16)] public string Kind { get; set; } = PayerKinds.Patient;
    /// <summary>Юридична особа платника (страхова компанія, підприємство).</summary>
    [MaxLength(256)] public string? OrganizationName { get; set; }
    [MaxLength(16)] public string? Edrpou { get; set; }
    [MaxLength(64)] public string? ContractNumber { get; set; }
    public DateTime? ContractDate { get; set; }
    public DateTime? ContractValidTo { get; set; }
    /// <summary>Прайс-лист платника = перелік покритих послуг і їхні ціни. Позиції поза прайсом сплачує пацієнт за роздрібним прайсом.</summary>
    [MaxLength(64)] public string? PriceListId { get; set; }
    /// <summary>Частка, яку сплачує платник, % (100 — повне покриття; 80 — пацієнт доплачує 20 %).</summary>
    public decimal CoveragePct { get; set; } = 100;
    /// <summary>Франшиза на замовлення, грн: першу суму покритих послуг сплачує пацієнт.</summary>
    public decimal FranchiseAmount { get; set; }
    /// <summary>Потрібен чинний поліс пацієнта (страхові програми).</summary>
    public bool RequiresPolicy { get; set; }
    /// <summary>Потрібне гарантійне лист/погодження страхової на кожне замовлення.</summary>
    public bool RequiresAuthorization { get; set; }
    /// <summary>Програма медичних гарантій (ehe_medical_service_program.id) для НСЗУ.</summary>
    [MaxLength(64)] public string? MedicalProgramId { get; set; }
    /// <summary>Договір MedLink (ehd_contract.id), якщо ведеться в МІС.</summary>
    [MaxLength(64)] public string? MedlinkContractId { get; set; }
    [MaxLength(256)] public string? ContactPerson { get; set; }
    [MaxLength(64)] public string? Phone { get; set; }
    [MaxLength(128)] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(PriceListId))] public LabPriceList? PriceList { get; set; }
}

/// <summary>[MedLink-new] mis_patient_insurance — страховий поліс пацієнта (загальна сутність МІС: прийоми, стаціонар, лабораторія).</summary>
[Table("mis_patient_insurance")]
public class MisPatientInsurance : GuidEntity
{
    [MaxLength(64)] public string PatientCardId { get; set; } = "";
    /// <summary>Страхова програма (у ЛІС — lab_payer з kind=INSURANCE; у MedLink — довідник страхових програм).</summary>
    [MaxLength(64)] public string PayerId { get; set; } = "";
    [MaxLength(64)] public string PolicyNumber { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    [MaxLength(512)] public string? Note { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(PatientCardId))] public MisPatientCard? PatientCard { get; set; }
    [ForeignKey(nameof(PayerId))] public LabPayer? Payer { get; set; }
    public bool IsValidAt(DateTime at) => IsActive && RecordState != RecordStates.Deleted && (ValidFrom == null || ValidFrom.Value.Date <= at.Date) && (ValidTo == null || ValidTo.Value.Date >= at.Date);
}

/// <summary>[ЛІС] lab_order_charge — нарахування за позицією замовлення: послуга/пакет, прайс, сума, частка платника і пацієнта.</summary>
[Table("lab_order_charge")]
public class LabOrderCharge : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? ProfileId { get; set; }
    [MaxLength(64)] public string? TestId { get; set; }
    [MaxLength(64)] public string? PackageId { get; set; }
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(512)] public string Name { get; set; } = "";
    /// <summary>Платник позиції (для непокритих позицій — пацієнт).</summary>
    [MaxLength(64)] public string PayerId { get; set; } = "";
    [MaxLength(16)] public string PayerKind { get; set; } = PayerKinds.Patient;
    [MaxLength(64)] public string? PriceListId { get; set; }
    public decimal Amount { get; set; }
    public decimal PayerAmount { get; set; }
    public decimal PatientAmount { get; set; }
    /// <summary>Позиція поза прайсом основного платника → сплачує пацієнт.</summary>
    public bool IsNotCovered { get; set; }
    /// <summary>Пацієнт обрав сплатити сам (позиція переведена на касу вручну).</summary>
    public bool IsPatientChoice { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    [ForeignKey(nameof(PayerId))] public LabPayer? Payer { get; set; }
}

/// <summary>[ЛІС] lab_payment — оплата пацієнта за замовленням (каса; у MedLink — касовий модуль/РРО, R2).</summary>
[Table("lab_payment")]
public class LabPayment : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>CASH | CARD | TRANSFER | REFUND</summary>
    [MaxLength(16)] public string Method { get; set; } = "CASH";
    [MaxLength(32)] public string ReceiptNumber { get; set; } = "";
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? CashierId { get; set; }
    [MaxLength(512)] public string? Note { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
}

/// <summary>[ЛІС] lab_invoice — рахунок платнику (страховій/підприємству/клініці) за замовленням або за період.</summary>
[Table("lab_invoice")]
public class LabInvoice : GuidEntity
{
    [MaxLength(32)] public string Number { get; set; } = "";
    [MaxLength(64)] public string PayerId { get; set; } = "";
    /// <summary>Для рахунку за одне замовлення; null — зведений за період.</summary>
    [MaxLength(64)] public string? OrderId { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public decimal Amount { get; set; }
    /// <summary>ISSUED | PAID | CANCELLED</summary>
    [MaxLength(16)] public string Status { get; set; } = "ISSUED";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    /// <summary>Ідентифікатори нарахувань, включених до рахунку (JSON).</summary>
    public string ChargeIdsJson { get; set; } = "[]";

    [ForeignKey(nameof(PayerId))] public LabPayer? Payer { get; set; }
}
