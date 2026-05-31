using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Serilog;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.Utils;

public static class PopulateDb
{
    private static readonly PasswordHasher<UserModel> _hasher = new();

    public static void SeedDb(AppDbContext db)
    {
        List<UserModel> users = CreateFakeUsers();
        db.Users.AddRange(users);
        List<Tickets> tickets = CreateFakeTickets(users);
        db.Tickets.AddRange(tickets);
        db.SaveChanges();
        Log.Information("Seeded database with {UserCount} users and {TicketCount} tickets", users.Count, tickets.Count);
    }

    public static List<UserModel> CreateFakeUsers()
    {
        var fakeUsers = new List<UserModel>
        {
            // Admins
            new() { Email = "admin@admin.sk", PasswordHash = _hasher.HashPassword(null, "Admin123!"), IsAdmin = true },
            new() { Email = "admin2@admin.sk", PasswordHash = _hasher.HashPassword(null, "Admin123!"), IsAdmin = true },

            // Regular users
            new()
            {
                Email = "jan.novak@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "peter.horvath@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "maria.kovacova@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "tomas.varga@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "anna.toth@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "martin.balaz@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "eva.suchanek@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
            new()
            {
                Email = "michal.urban@example.sk", PasswordHash = _hasher.HashPassword(null, "User123!"), IsAdmin = false
            },
        };
        return fakeUsers;
    }

    public static List<Tickets> CreateFakeTickets(List<UserModel> users)
    {
        var regularUsers = users.Where(u => u.IsAdmin == false).ToList();
        var adminIds = users.Where(u => u.IsAdmin == true).Select(u => u.Id).ToList();

        var fakeTickets = new List<Tickets>
        {
            // jan.novak
            new()
            {
                Predmet = "Nefunguje prihlasenie", TicketText = "Pri pokuse o prihlasenie sa zobrazuje chyba 401.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[0].Id,
                RelatedProject = "AIS-autologin"
            },
            new()
            {
                Predmet = "Pomaly nacitavanie", TicketText = "Dashboard sa nacitava velmi pomaly, asi 10 sekund.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[0].Id,
                AssignedToUserId = adminIds[0]
            },
            new()
            {
                Predmet = "Ziadost o dark mode", TicketText = "Bolo by mozne pridat dark mode do aplikacie?",
                TicketType = TicketTypeEnum.Feature, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Unassigned,
                CreatedByUserId = regularUsers[0].Id
            },

            // peter.horvath
            new()
            {
                Predmet = "Chyba pri platbe", TicketText = "Transakcia kartou nepresla, hoci udaje su spravne.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[1].Id,
                RelatedProject = "cardmarket-autologin"
            },
            new()
            {
                Predmet = "Chyba 500 pri ukladani",
                TicketText = "Pri ulozeni novej faktury dostavam Internal Server Error.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[1].Id,
                AssignedToUserId = adminIds[1]
            },
            new()
            {
                Predmet = "Export PDF prazdny",
                TicketText = "Export do PDF generuje prazdny subor vo Firefoxe aj Chrome.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Completed, CreatedByUserId = regularUsers[1].Id,
                AssignedToUserId = adminIds[0]
            },
            new()
            {
                Predmet = "Otazka k objednavke",
                TicketText = "Mozem dostat detaily k mojej poslednej objednavke c. 12345?",
                TicketType = TicketTypeEnum.Question, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Completed,
                CreatedByUserId = regularUsers[1].Id, AssignedToUserId = adminIds[0]
            },

            // maria.kovacova
            new()
            {
                Predmet = "Karta sa nezobrazuje v sklade",
                TicketText = "Pridal som novu kartu, ale neukazuje sa v zozname skladu.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[2].Id,
                RelatedProject = "pokemon_pricer"
            },
            new()
            {
                Predmet = "Zly vypocet DPH", TicketText = "DPH sa pocita zle ked je polozka oslobodena od dane.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[2].Id,
                AssignedToUserId = adminIds[1]
            },
            new()
            {
                Predmet = "Nefunguje vyhladavanie",
                TicketText = "Vyhladavanie podla mena zakaznika nevracia ziadne vysledky.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[2].Id
            },

            // tomas.varga
            new()
            {
                Predmet = "Nemozem pridat zakaznika",
                TicketText = "Formular na pridanie zakaznika ignoruje moje vstupy.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[3].Id
            },
            new()
            {
                Predmet = "Chyba pri importe CSV", TicketText = "Pri importe CSV s diakritikou sa znaky rozbijaju.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[3].Id,
                AssignedToUserId = adminIds[0]
            },
            new()
            {
                Predmet = "Shopify integracia", TicketText = "Ako mozem prepojit Shopify obchod s touto aplikaciou?",
                TicketType = TicketTypeEnum.Question, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Unassigned,
                CreatedByUserId = regularUsers[3].Id, RelatedProject = "tradeTracker"
            },
            new()
            {
                Predmet = "Tlac faktury nereaguje", TicketText = "Tlacidlo Tlac fakturu nereaguje na klik v Safari.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Completed, CreatedByUserId = regularUsers[3].Id,
                AssignedToUserId = adminIds[1]
            },

            // anna.toth
            new()
            {
                Predmet = "Historia transakcii chyba",
                TicketText = "Historia transakcii sa nezobrazuje pre zaznamy starsie ako 6 mesiacov.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[4].Id
            },
            new()
            {
                Predmet = "Notifikacie nefunguju", TicketText = "Emailove notifikacie o novych ticketoch mi nechodia.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Low,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[4].Id
            },
            new()
            {
                Predmet = "Ziadost o hromadny export",
                TicketText = "Bolo by mozne exportovat vsetky faktury naraz do ZIP?",
                TicketType = TicketTypeEnum.Feature, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Unassigned,
                CreatedByUserId = regularUsers[4].Id
            },

            // martin.balaz
            new()
            {
                Predmet = "Duplicitne zaznamy", TicketText = "Po importe CSV sa niektore polozky zdvojili.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[5].Id,
                AssignedToUserId = adminIds[0]
            },
            new()
            {
                Predmet = "Problem s rolami", TicketText = "Uzivatel s rolou viewer moze menit data, co by nemal.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Critical,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[5].Id
            },
            new()
            {
                Predmet = "UI bug v mobile", TicketText = "Na mobile sa tlacidla prekryvaju v detaile faktury.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Low,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[5].Id
            },

            // eva.suchanek
            new()
            {
                Predmet = "Reset hesla nefunguje", TicketText = "Email na reset hesla mi neprisiel ani po 30 minutach.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[6].Id,
                AssignedToUserId = adminIds[1]
            },
            new()
            {
                Predmet = "Filtrovanie podla datumu",
                TicketText = "Filter podla datumu v prehlade objednavok nefunguje spravne.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[6].Id
            },
            new()
            {
                Predmet = "Pridanie poznamky k ticketu",
                TicketText = "Chcel by som vediet pridat internu poznamku k ticketu.",
                TicketType = TicketTypeEnum.Feature, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Unassigned,
                CreatedByUserId = regularUsers[6].Id
            },

            // michal.urban
            new()
            {
                Predmet = "Timeout pri nahravani suboru",
                TicketText = "Nahravanie prilohy vacsej ako 5MB konci timeoutom.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Medium,
                Status = TicketStatusEnum.Unassigned, CreatedByUserId = regularUsers[7].Id
            },
            new()
            {
                Predmet = "Neplatny token pri API", TicketText = "API volanie vracia 403 hoci token je aktualny.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.High,
                Status = TicketStatusEnum.Inprogress, CreatedByUserId = regularUsers[7].Id,
                AssignedToUserId = adminIds[0]
            },
            new()
            {
                Predmet = "Zly format datumu",
                TicketText = "Datum sa zobrazuje v americkom formate MM/DD namiesto DD.MM.",
                TicketType = TicketTypeEnum.BugReport, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Completed,
                CreatedByUserId = regularUsers[7].Id, AssignedToUserId = adminIds[1]
            },
            new()
            {
                Predmet = "Hromadne mazanie",
                TicketText = "Chcel by som vediet oznacit a zmazat viacero zaznamov naraz.",
                TicketType = TicketTypeEnum.Feature, Severity = SeverityEnum.Low, Status = TicketStatusEnum.Unassigned,
                CreatedByUserId = regularUsers[7].Id
            },
        };
        return fakeTickets;
    }
}