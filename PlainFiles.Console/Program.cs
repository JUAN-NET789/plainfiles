using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PlainFiles.Core;

Console.WriteLine("Iniciando aplicación...");

var manualCsv = new ManualCsvHelper();
using var log = new LogWriter("log.txt");

// Ensure Users.txt exists and has at least one user example
const string usersPath = "Users.txt";
var users = manualCsv.ReadCsv(usersPath);
if (users.Count == 0)
{
    users.Add(new[] { "jzuluaga", "P@ssw0rd123!", "true" });
    users.Add(new[] { "mbedoya", "S0yS3gur02025*", "false" });
    manualCsv.WriteCsv(usersPath, users);
    log.WriteLog("INFO", $"SYSTEM - Created initial {usersPath}");
}

// Authenticate user
string currentUser = string.Empty;
int attempts = 0;
const int maxAttempts = 3;
bool authenticated = false;

while (!authenticated && attempts < maxAttempts)
{
    Console.Write("Usuario: ");
    var username = Console.ReadLine()?.Trim() ?? string.Empty;
    Console.Write("Contraseña: ");
    var password = ReadPassword();

    var userRecordIndex = users.FindIndex(u => u.Length >= 1 && u[0].Equals(username, StringComparison.OrdinalIgnoreCase));
    if (userRecordIndex < 0)
    {
        attempts++;
        log.WriteLog("WARN", $"SYSTEM - Unknown login attempt for '{username}' (attempt {attempts})");
        Console.WriteLine("Usuario o contraseña incorrectos.");
        continue;
    }

    var userRecord = users[userRecordIndex].ToList();
    while (userRecord.Count < 3) userRecord.Add("false");
    users[userRecordIndex] = userRecord.ToArray();

    var isActive = bool.TryParse(userRecord[2], out var activeVal) && activeVal;
    if (!isActive)
    {
        Console.WriteLine("Usuario bloqueado. Contacte al administrador.");
        log.WriteLog("WARN", $"SYSTEM - Blocked user '{username}' attempted login");
        Environment.Exit(0);
    }

    if (userRecord[1] == password)
    {
        authenticated = true;
        currentUser = userRecord[0];
        log.WriteLog("INFO", $"{currentUser} - Login successful");
        Console.WriteLine($"Bienvenido, {currentUser}.");
        break;
    }
    else
    {
        attempts++;
        log.WriteLog("WARN", $"{username} - Invalid password (attempt {attempts})");
        Console.WriteLine("Usuario o contraseña incorrectos.");
        if (attempts >= maxAttempts)
        {
            // lock the user
            users[userRecordIndex][2] = "false";
            manualCsv.WriteCsv(usersPath, users);
            log.WriteLog("WARN", $"{username} - Account locked after {maxAttempts} failed attempts");
            Console.WriteLine("Se han agotado los intentos. Usuario bloqueado.");
            Environment.Exit(0);
        }
    }
}

if (!authenticated)
{
    Console.WriteLine("No se pudo autenticar. Saliendo...");
    Environment.Exit(0);
}

// People CSV handling
Console.Write("Digite el nombre de la lista (por defecto 'people'): ");
var listName = Console.ReadLine();
if (string.IsNullOrEmpty(listName))
{
    listName = "people";
}

var peoplePath = $"{listName}.csv";
var people = manualCsv.ReadCsv(peoplePath);

// Normalize records to expected schema: Id,Nombre,Apellido,Telefono,Ciudad,Saldo
for (int i = 0; i < people.Count; i++)
{
    var rec = people[i].ToList();
    while (rec.Count < 6) rec.Add(string.Empty);
    people[i] = rec.ToArray();
}

string option;
do
{
    option = MyMenu();
    Console.WriteLine();
    Console.WriteLine();
    switch (option)
    {
        case "1":
            AddPerson();
            break;

        case "2":
            ListPeople();
            break;

        case "3":
            SaveFile();
            break;

        case "4":
            DeletePerson();
            break;

        case "5":
            SortData();
            break;

        case "6":
            EditPerson();
            break;

        case "7":
            ReportByCity();
            break;

        case "0":
            Console.WriteLine("Saliendo...");
            log.WriteLog("INFO", $"{currentUser} - Exit application");
            break;

        default:
            Console.WriteLine("Opción no válida.");
            break;
    }
} while (option != "0");

string MyMenu()
{
    Console.WriteLine();
    Console.WriteLine("1. Adicionar.");
    Console.WriteLine("2. Mostrar.");
    Console.WriteLine("3. Grabar.");
    Console.WriteLine("4. Eliminar.");
    Console.WriteLine("5. Ordenar.");
    Console.WriteLine("6. Editar.");
    Console.WriteLine("7. Informe por Ciudad.");
    Console.WriteLine("0. Salir.");
    Console.Write("Seleccione una opción: ");
    return Console.ReadLine() ?? string.Empty;
}

void AddPerson()
{
    Console.WriteLine("Adicionar persona:");

    // ID: must be numeric and unique
    string idStr;
    while (true)
    {
        Console.Write("ID (único, numérico): ");
        idStr = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!int.TryParse(idStr, out _))
        {
            Console.WriteLine("ID debe ser un número.");
            continue;
        }
        if (people.Any(p => p.Length > 0 && p[0] == idStr))
        {
            Console.WriteLine("ID ya existe. Ingrese otro.");
            continue;
        }
        break;
    }

    // Name
    string name;
    do
    {
        Console.Write("Nombres: ");
        name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) Console.WriteLine("El nombre es obligatorio.");
    } while (string.IsNullOrEmpty(name));

    // LastName
    string last;
    do
    {
        Console.Write("Apellidos: ");
        last = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(last)) Console.WriteLine("El apellido es obligatorio.");
    } while (string.IsNullOrEmpty(last));

    // Phone validation
    string phone;
    var phoneRegex = new Regex(@"^[\d\-\+\s\(\)]{7,}$");
    do
    {
        Console.Write("Teléfono: ");
        phone = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(phone) || !phoneRegex.IsMatch(phone))
            Console.WriteLine("Teléfono inválido. Debe contener al menos 7 dígitos y solo números, espacios, +, -, ().");
    } while (string.IsNullOrEmpty(phone) || !phoneRegex.IsMatch(phone));

    // City
    Console.Write("Ciudad: ");
    var city = Console.ReadLine()?.Trim() ?? string.Empty;

    // Balance positive number
    string balanceStr;
    decimal balance;
    do
    {
        Console.Write("Saldo (número positivo): ");
        balanceStr = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!decimal.TryParse(balanceStr, NumberStyles.Number, CultureInfo.CurrentCulture, out balance) || balance < 0)
        {
            Console.WriteLine("Saldo inválido. Ingrese un número positivo.");
        }
    } while (!decimal.TryParse(balanceStr, NumberStyles.Number, CultureInfo.CurrentCulture, out balance) || balance < 0);

    var record = new[] { idStr, name, last, phone, city, balance.ToString("N2", CultureInfo.CurrentCulture) };
    people.Add(record);
    Console.WriteLine("Persona adicionada.");
    log.WriteLog("INFO", $"{currentUser} - Added person ID {idStr}");
}

void ListPeople()
{
    Console.WriteLine("Lista de personas:");
    Console.WriteLine("ID\tNombres\tApellidos\tTeléfono\tCiudad\tSaldo");
    foreach (var person in people)
    {
        Console.WriteLine($"{person[0]}\t{person[1]}\t{person[2]}\t{person[3]}\t{person[4]}\t{person[5]}");
    }
    log.WriteLog("INFO", $"{currentUser} - Listed people ({people.Count} records)");
}

void ReportByCity()
{
    Console.WriteLine();
    var culture = CultureInfo.CurrentCulture;
    var groups = people.GroupBy(p => (p.Length > 4 ? p[4] : string.Empty) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    decimal totalGeneral = 0m;
    foreach (var g in groups.OrderBy(g => g.Key))
    {
        var cityName = string.IsNullOrWhiteSpace(g.Key) ? "(Sin ciudad)" : g.Key;
        Console.WriteLine($"Ciudad: {cityName}");
        Console.WriteLine();
        Console.WriteLine("ID\tNombres\tApellidos\tSaldo");
        Console.WriteLine("—\t—-------------\t—------------\t—----------");

        decimal subtotal = 0m;
        foreach (var p in g)
        {
            var id = p.Length > 0 ? p[0] : string.Empty;
            var nm = p.Length > 1 ? p[1] : string.Empty;
            var ln = p.Length > 2 ? p[2] : string.Empty;
            var saldoStr = p.Length > 5 ? p[5] : "0";
            if (!decimal.TryParse(saldoStr, NumberStyles.Number, culture, out var saldo)) saldo = 0m;
            subtotal += saldo;
            Console.WriteLine($"{id}\t{nm}\t{ln}\t{saldo.ToString("N2", culture)}");
        }

        Console.WriteLine("\t\t\t\t========");
        Console.WriteLine($"Total: {cityName}\t\t{subtotal.ToString("N2", culture)}");
        Console.WriteLine();
        totalGeneral += subtotal;
    }

    Console.WriteLine("\t\t\t\t========");
    Console.WriteLine($"Total General:\t\t{totalGeneral.ToString("N2", culture)}");
    log.WriteLog("INFO", $"{currentUser} - Generated report by city");
}

void EditPerson()
{
    Console.Write("Digite el ID de la persona a editar: ");
    var id = Console.ReadLine()?.Trim() ?? string.Empty;
    var index = people.FindIndex(p => p.Length > 0 && p[0] == id);
    if (index < 0)
    {
        Console.WriteLine("ID no existe.");
        return;
    }

    var person = people[index];
    Console.WriteLine("Presione ENTER para mantener el valor previo.");

    Console.Write($"Nombres ({person[1]}): ");
    var name = Console.ReadLine();
    if (!string.IsNullOrEmpty(name)) person[1] = name.Trim();

    Console.Write($"Apellidos ({person[2]}): ");
    var last = Console.ReadLine();
    if (!string.IsNullOrEmpty(last)) person[2] = last.Trim();

    // Phone validation - allow ENTER to keep
    var phoneRegex = new Regex(@"^[\d\-\+\s\(\)]{7,}$");
    while (true)
    {
        Console.Write($"Teléfono ({person[3]}): ");
        var phone = Console.ReadLine();
        if (string.IsNullOrEmpty(phone)) break;
        if (!phoneRegex.IsMatch(phone.Trim()))
        {
            Console.WriteLine("Teléfono inválido.");
            continue;
        }
        person[3] = phone.Trim();
        break;
    }

    Console.Write($"Ciudad ({person[4]}): ");
    var city = Console.ReadLine();
    if (!string.IsNullOrEmpty(city)) person[4] = city.Trim();

    // Balance
    while (true)
    {
        Console.Write($"Saldo ({person[5]}): ");
        var balIn = Console.ReadLine();
        if (string.IsNullOrEmpty(balIn)) break;
        if (!decimal.TryParse(balIn.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var bal) || bal < 0)
        {
            Console.WriteLine("Saldo inválido. Debe ser número positivo.");
            continue;
        }
        person[5] = bal.ToString("N2", CultureInfo.CurrentCulture);
        break;
    }

    people[index] = person;
    Console.WriteLine("Persona actualizada.");
    log.WriteLog("INFO", $"{currentUser} - Edited person ID {id}");
}

void DeletePerson()
{
    Console.Write("Digite el ID de la persona a borrar: ");
    var id = Console.ReadLine()?.Trim() ?? string.Empty;
    var person = people.FirstOrDefault(p => p.Length > 0 && p[0] == id);
    if (person == null)
    {
        Console.WriteLine("ID no existe.");
        return;
    }

    Console.WriteLine($"ID: {person[0]} - {person[1]} {person[2]} - Tel: {person[3]} - Ciudad: {person[4]} - Saldo: {person[5]}");
    Console.Write("Confirma borrado? (S/N): ");
    var confirm = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();
    if (confirm == "S" || confirm == "Y")
    {
        people.Remove(person);
        Console.WriteLine("Persona borrada.");
        log.WriteLog("INFO", $"{currentUser} - Deleted person ID {id}");
    }
    else
    {
        Console.WriteLine("Operación cancelada.");
    }
}

void SaveFile()
{
    manualCsv.WriteCsv(peoplePath, people);
    Console.WriteLine("Archivo guardado.");
    log.WriteLog("INFO", $"{currentUser} - Saved file '{peoplePath}'");
}

static string ReadPassword()
{
    var pass = string.Empty;
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
        {
            pass = pass[0..^1];
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            pass += key.KeyChar;
            Console.Write("*");
        }
    }
    Console.WriteLine();
    return pass;
}

void SortData()
{
    int order;
    do
    {
        Console.Write("Por cual campo desea ordenar 0. Nombre, 1. Apellido, 2. Saldo? ");
        var orderString = Console.ReadLine();
        int.TryParse(orderString, out order);
        if (order < 0 || order > 2)
        {
            Console.WriteLine("Orden no válido. Intente de nuevo.");
        }
    } while (order < 0 || order > 2);

    int type;
    do
    {
        Console.Write("Desea ordenar 0. Ascendente, 1. Descendente? ");
        var typeString = Console.ReadLine();
        int.TryParse(typeString, out type);
        if (type < 0 || type > 1)
        {
            Console.WriteLine("Orden no válido. Intente de nuevo.");
        }
    } while (type < 0 || type > 1);

    people.Sort((a, b) =>
    {
        int cmp;
        if (order == 2) // Saldo: comparar como número
        {
            bool parsedA = decimal.TryParse(a[5], NumberStyles.Number, CultureInfo.CurrentCulture, out var balA);
            bool parsedB = decimal.TryParse(b[5], NumberStyles.Number, CultureInfo.CurrentCulture, out var balB);

            if (!parsedA) balA = decimal.MinValue;
            if (!parsedB) balB = decimal.MinValue;

            cmp = balA.CompareTo(balB);
        }
        else // Nombre o Apellido
        {
            cmp = string.Compare(a[order + 1], b[order + 1], StringComparison.OrdinalIgnoreCase);
        }

        return type == 0 ? cmp : -cmp; // 0 = ascendente, 1 = descendente
    });

    Console.WriteLine("Datos ordenados.");
    log.WriteLog("INFO", $"{currentUser} - Sorted data by {(order == 2 ? "Saldo" : order == 0 ? "Nombre" : "Apellido")} {(type == 0 ? "ASC" : "DESC")}");
}