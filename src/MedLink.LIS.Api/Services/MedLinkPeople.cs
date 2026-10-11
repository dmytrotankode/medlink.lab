// Створення/оновлення пацієнта у структурі MedLink: cmn_person (ПІБ, ІПН, контакти) + mis_patient_card (картка закладу).
// Плоскі поля API (lastName, firstName, taxId…) відображаються на колонки evomis.
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Common;

namespace MedLink.LIS.Api.Services;

public static class MedLinkPeople
{
    /// <summary>Нова картка пацієнта разом з особою (cmn_person). Додати до контексту треба лише картку — особа додасться через навігацію.</summary>
    public static MisPatientCard NewPatient(NewPatientRequest req, string organizationId)
    {
        var person = new CmnPerson();
        var card = new MisPatientCard
        {
            Person = person, PersonId = person.Id, OrganizationId = organizationId, RegDate = DateTime.UtcNow,
            DocumentTypeId = MedLinkDefaults.PatientCardDocumentTypeId, PatientCardTypeId = MedLinkDefaults.PatientCardTypePersonId,
            PrivacyRequestTypeId = MedLinkEnums.PrivacyUnknown
        };
        Apply(card, req, isNew: true);
        return card;
    }

    /// <summary>Застосовує запит до особи та картки. Для оновлення порожні поля запиту не змінюють наявні значення.</summary>
    public static void Apply(MisPatientCard card, NewPatientRequest req, bool isNew)
    {
        var p = card.Person ?? throw new InvalidOperationException("Картку пацієнта завантажено без cmn_person");
        if (isNew || !string.IsNullOrWhiteSpace(req.LastName)) p.LastName = req.LastName?.Trim();
        if (isNew || !string.IsNullOrWhiteSpace(req.FirstName)) p.Name = req.FirstName?.Trim();
        if (isNew || req.SecondName != null) p.MiddleName = string.IsNullOrWhiteSpace(req.SecondName) ? null : req.SecondName.Trim();
        if (isNew || req.BirthDate != null) p.Birthday = req.BirthDate;
        if (isNew || !string.IsNullOrWhiteSpace(req.Gender)) p.GenderId = MedLinkEnums.GenderId(req.Gender);
        if (isNew || req.Phone != null) p.Phone = req.Phone;
        if (isNew || req.Email != null) p.Email = req.Email;
        if (isNew || req.TaxId != null) { p.Ipn = string.IsNullOrWhiteSpace(req.TaxId) ? null : req.TaxId.Trim(); p.NoIpn = p.Ipn == null; }
        if (isNew || req.Address != null) p.Location = req.Address;

        p.Caption = CmnPerson.BuildCaption(p.LastName, p.Name, p.MiddleName);
        card.Caption = p.Caption;
        card.Birthday = p.Birthday;
        card.GenderId = p.GenderId;
        card.Location = p.Location;
        card.LastNameLatin = TransliterationKmu2010.ToLatin(p.LastName ?? "");
        card.FirstNameLatin = TransliterationKmu2010.ToLatin(p.Name ?? "");
    }

    public static PatientDto ToDto(MisPatientCard c, DateTime? at = null)
    {
        var p = c.Person;
        return new PatientDto
        {
            Id = c.Id, LastName = p?.LastName ?? "", FirstName = p?.Name ?? "", SecondName = p?.MiddleName, FullName = c.Caption ?? "",
            LastNameLatin = c.LastNameLatin, FirstNameLatin = c.FirstNameLatin,
            BirthDate = c.Birthday, AgeYears = c.Birthday.HasValue ? Core.Clinical.AgeUnits.AgeYears(c.Birthday.Value, at ?? DateTime.UtcNow) : null,
            Gender = c.Gender, Phone = p?.Phone, Email = p?.Email, TaxId = p?.Ipn, Address = p?.Location ?? c.Location
        };
    }
}

/// <summary>Значення за замовчуванням для автономної ЛІС (один заклад). В evomis беруться з контексту користувача.</summary>
public static class MedLinkDefaults
{
    /// <summary>org_organization демо-закладу.</summary>
    public const string OrganizationId = "0a000000-0000-0000-0000-000000000001";
    /// <summary>cmn_enum_record MedicalDocumentType «Амбулаторна карта» (AmbCard) — як в evomis DbInitializer.</summary>
    public const string PatientCardDocumentTypeId = "d95d3e30-15ea-421c-8785-8e80e60ffbbe";
    /// <summary>cmn_enum_record PatientCardType «person» (в evomis створюється скриптом v002.30 без фіксованого id; при перенесенні зіставляти за code).</summary>
    public const string PatientCardTypePersonId = "0c000000-0000-0000-0000-00000000e001";
    /// <summary>ehe_medical_referral_category «laboratory_procedure» (в evomis — довідник eHealth; зіставляти за code).</summary>
    public const string ReferralCategoryLaboratoryId = "0c000000-0000-0000-0000-00000000e002";
    /// <summary>cmn_enum_record статусу направлення «active» (зіставляти за code).</summary>
    public const string ReferralStatusActiveId = MedLinkEnums.ReferralStatusActive;
}
