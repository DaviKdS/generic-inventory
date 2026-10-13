using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using GenericInventory.Data;
using GenericInventory.Rooms.Dtos;
using GenericInventory.Rooms.Entities;

namespace GenericInventory.Rooms.Services;

public class BedReservationsService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "available",
        "reserved",
        "occupied",
        "cleaning",
        "maintenance"
    };

    private readonly AppDbContext _db;

    public BedReservationsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BedReservationsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        var beds = await _db.Beds
            .AsNoTracking()
            .Select(bed => BedDto.FromEntity(bed))
            .ToListAsync(cancellationToken);
        beds = beds
            .OrderBy(bed => bed.Fixed ? 0 : 1)
            .ThenBy(bed => BedNumber(bed.Label))
            .ThenBy(bed => bed.Label)
            .ToList();
        var history = await _db.BedHistory
            .AsNoTracking()
            .Select(entry => BedHistoryDto.FromEntity(entry))
            .ToListAsync(cancellationToken);
        history = history
            .OrderByDescending(entry => entry.Date)
            .Take(60)
            .ToList();

        return new BedReservationsDto
        {
            Areas = new List<BedAreaDto>
            {
                new()
                {
                    Id = "camas",
                    Name = "Camas",
                    Rooms = beds
                }
            },
            History = history
        };
    }

    public async Task<BedDto> CreateAsync(BedCreateDto form, string changedBy, CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var label = SanitizeBedLabel(form.RoomLabel);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = await NextBedLabelAsync(cancellationToken);
        }

        await EnsureUniqueLabelAsync(label, null, cancellationToken);
        var bed = new Bed
        {
            Id = await NextBedIdAsync(label, cancellationToken),
            Label = label,
            Category = SanitizeText(form.Category, 80, "Leito"),
            Capacity = Math.Clamp(form.Capacity, 1, 99),
            Status = "available",
            Fixed = false,
            OccupancyStartedAt = null,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Beds.Add(bed);
        AddHistory(bed, "Cama adicionada", changedBy);
        await _db.SaveChangesAsync(cancellationToken);
        await TrimHistoryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BedDto.FromEntity(bed);
    }

    public async Task<BedDto> UpdateAsync(
        string id,
        BedUpdateDto form,
        string changedBy,
        IReadOnlySet<string>? hiddenFields = null,
        CancellationToken cancellationToken = default)
    {
        hiddenFields ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await EnsureSeedAsync(cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var bed = await _db.Beds.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cama não encontrada.");

        var label = SanitizeBedLabel(form.RoomLabel);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = bed.Label;
        }
        await EnsureUniqueLabelAsync(label, bed.Id, cancellationToken);

        var wasInUse = IsInUseStatus(bed.Status);
        bed.Label = label;
        bed.Category = SanitizeText(form.Category, 80, "Leito");
        bed.Capacity = Math.Clamp(form.Capacity, 1, 99);
        bed.Status = ValidStatuses.Contains(form.Status) ? form.Status.ToLowerInvariant() : "available";
        if (!wasInUse && IsInUseStatus(bed.Status))
        {
            bed.OccupancyStartedAt = DateTimeOffset.UtcNow;
        }
        else if (!IsInUseStatus(bed.Status))
        {
            bed.OccupancyStartedAt = null;
        }
        else if (bed.OccupancyStartedAt is null)
        {
            bed.OccupancyStartedAt = bed.UpdatedAt;
        }
        bed.GuestName = hiddenFields.Contains("guestName") ? bed.GuestName : SanitizeText(form.GuestName, 120);
        bed.GuestCode = hiddenFields.Contains("guestCode") ? bed.GuestCode : SanitizeCode(form.ReservationCode);
        bed.Cpf = hiddenFields.Contains("cpf") ? bed.Cpf : SanitizeDigits(form.Cpf, 11);
        bed.Phone = hiddenFields.Contains("phone") ? bed.Phone : SanitizeText(form.Phone, 32);
        bed.EmergencyContact = hiddenFields.Contains("emergencyContact") ? bed.EmergencyContact : SanitizeText(form.EmergencyContact, 120);
        bed.EmergencyPhone = hiddenFields.Contains("emergencyPhone") ? bed.EmergencyPhone : SanitizeText(form.EmergencyPhone, 32);
        bed.BloodType = hiddenFields.Contains("bloodType") ? bed.BloodType : SanitizeText(form.BloodType, 8).ToUpperInvariant();
        bed.CheckIn = hiddenFields.Contains("checkIn") ? bed.CheckIn : NormalizeDate(form.CheckIn);
        bed.CheckOut = hiddenFields.Contains("checkOut") ? bed.CheckOut : NormalizeDate(form.CheckOut);
        bed.ActualCheckOut = hiddenFields.Contains("actualCheckOut") ? bed.ActualCheckOut : NormalizeDate(form.ActualCheckOut);
        bed.Note = hiddenFields.Contains("note") ? bed.Note : SanitizeText(form.Note, 240);
        bed.Operator = hiddenFields.Contains("operator") ? bed.Operator : SanitizeText(form.Operator, 120, changedBy);

        if (IsInUseStatus(bed.Status) && string.IsNullOrWhiteSpace(bed.CheckIn))
        {
            bed.CheckIn = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        }

        if (IsInUseStatus(bed.Status) && string.IsNullOrWhiteSpace(bed.GuestCode))
        {
            bed.GuestCode = $"RSV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000000:000000}";
        }

        bed.UpdatedAt = DateTimeOffset.UtcNow;
        AddHistory(bed, bed.Note, bed.Operator);
        await _db.SaveChangesAsync(cancellationToken);
        await TrimHistoryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BedDto.FromEntity(bed);
    }

    public async Task DeleteAsync(string id, string changedBy, CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var bed = await _db.Beds.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cama não encontrada.");

        if (bed.Fixed)
        {
            throw new InvalidOperationException("As camas fixas de Cama 1 a Cama 24 não podem ser excluídas.");
        }

        _db.Beds.Remove(bed);
        AddHistory(bed, "Cama adicional excluída", changedBy);
        await _db.SaveChangesAsync(cancellationToken);
        await TrimHistoryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetDemoAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.BedHistory.RemoveRange(_db.BedHistory);
        _db.Beds.RemoveRange(_db.Beds);
        await _db.SaveChangesAsync(cancellationToken);
        await SeedDefaultBedsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureSeedAsync(CancellationToken cancellationToken)
    {
        if (await _db.Beds.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        if (!await _db.Beds.AnyAsync(cancellationToken))
        {
            await SeedDefaultBedsAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedDefaultBedsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var number in Enumerable.Range(1, 24))
        {
            var seed = SeedFor(number);
            _db.Beds.Add(new Bed
            {
                Id = $"cama-{number}",
                Label = $"Cama {number}",
                Category = "Leito",
                Capacity = 1,
                Status = seed.Status,
                GuestCode = seed.GuestCode,
                CheckIn = seed.CheckInOffset == null ? string.Empty : DateOnly.FromDateTime(DateTime.Today.AddDays(seed.CheckInOffset.Value)).ToString("yyyy-MM-dd"),
                CheckOut = seed.CheckOutOffset == null ? string.Empty : DateOnly.FromDateTime(DateTime.Today.AddDays(seed.CheckOutOffset.Value)).ToString("yyyy-MM-dd"),
                Note = seed.Note,
                Fixed = true,
                OccupancyStartedAt = IsInUseStatus(seed.Status) ? now : null,
                UpdatedAt = now
            });
        }

        _db.BedHistory.Add(new BedHistory
        {
            Date = now,
            AreaName = "Camas",
            RoomLabel = "Cama 22",
            Status = "occupied",
            GuestCode = "RSV-2402",
            Note = "Saída prevista para hoje"
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static (string Status, string GuestCode, int? CheckInOffset, int? CheckOutOffset, string Note) SeedFor(int number)
    {
        if (new[] { 2, 7, 13, 19 }.Contains(number)) return ("reserved", $"RSV-{2400 + number}", 1, 3, string.Empty);
        if (new[] { 3, 9, 16, 22 }.Contains(number)) return ("occupied", $"RSV-{2380 + number}", -2, 1, string.Empty);
        if (new[] { 5, 18 }.Contains(number)) return ("cleaning", string.Empty, null, null, "Limpeza programada");
        if (new[] { 12, 24 }.Contains(number)) return ("maintenance", string.Empty, null, null, "Manutenção preventiva");
        return ("available", string.Empty, null, null, string.Empty);
    }

    private async Task<string> NextBedLabelAsync(CancellationToken cancellationToken)
    {
        var labels = await _db.Beds.Select(bed => bed.Label).ToListAsync(cancellationToken);
        var next = labels
            .Select(BedNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"Cama {next}";
    }

    private async Task<string> NextBedIdAsync(string label, CancellationToken cancellationToken)
    {
        var baseId = Slugify(label);
        var id = baseId;
        var suffix = 2;
        while (await _db.Beds.AnyAsync(bed => bed.Id == id, cancellationToken))
        {
            id = $"{baseId}-{suffix++}";
        }

        return id;
    }

    private async Task EnsureUniqueLabelAsync(string label, string? currentId, CancellationToken cancellationToken)
    {
        if (await _db.Beds.AnyAsync(
                bed => bed.Id != currentId && bed.Label.ToLower() == label.ToLower(),
                cancellationToken))
        {
            throw new InvalidOperationException("Já existe uma cama com esta identificação.");
        }
    }

    private void AddHistory(Bed bed, string note, string changedBy)
    {
        _db.BedHistory.Add(new BedHistory
        {
            Date = bed.UpdatedAt,
            BedId = bed.Id,
            AreaName = "Camas",
            RoomLabel = bed.Label,
            Status = bed.Status,
            GuestCode = bed.GuestCode,
            Note = SanitizeText(note, 240),
            Operator = SanitizeText(changedBy, 120)
        });
    }

    private async Task TrimHistoryAsync(CancellationToken cancellationToken)
    {
        var history = await _db.BedHistory
            .AsNoTracking()
            .Select(entry => new { entry.Id, entry.Date })
            .ToListAsync(cancellationToken);
        var oldIds = history
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .Skip(60)
            .Select(entry => entry.Id)
            .ToList();
        if (oldIds.Count == 0)
        {
            return;
        }

        await _db.BedHistory
            .Where(entry => oldIds.Contains(entry.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static bool IsInUseStatus(string status)
    {
        return string.Equals(status, "reserved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "occupied", StringComparison.OrdinalIgnoreCase);
    }

    private static int BedNumber(string label)
    {
        var match = Regex.Match(label ?? string.Empty, @"Cama\s+(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    private static string SanitizeBedLabel(string value)
    {
        return SanitizeText(value, 48);
    }

    private static string SanitizeText(string value, int maxLength, string fallback = "")
    {
        var sanitized = Regex.Replace(value ?? string.Empty, @"[<>]", string.Empty);
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? fallback
            : sanitized[..Math.Min(maxLength, sanitized.Length)];
    }

    private static string SanitizeCode(string value)
    {
        var sanitized = Regex.Replace(value ?? string.Empty, @"[^a-zA-Z0-9._-]", string.Empty)
            .Trim()
            .ToUpperInvariant();
        return sanitized[..Math.Min(24, sanitized.Length)];
    }

    private static string SanitizeDigits(string value, int maxLength)
    {
        var digits = Regex.Replace(value ?? string.Empty, @"\D", string.Empty);
        return digits[..Math.Min(maxLength, digits.Length)];
    }

    private static string NormalizeDate(string value)
    {
        return DateOnly.TryParse(value, out var date) ? date.ToString("yyyy-MM-dd") : string.Empty;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var withoutMarks = new string(normalized.Where(ch =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(withoutMarks.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"cama-{Guid.NewGuid():N}" : slug;
    }
}
