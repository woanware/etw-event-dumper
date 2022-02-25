using etw_event_dumper;
using System.CommandLine;

/// <summary>
/// 
/// </summary>
Option providersFile = new Option<string>(new[] { "-p", "--providers" },
                                      description: "File path for file containing ETW provider guids")
{
    IsRequired = true
};

/// <summary>
/// 
/// </summary>

Option outputFile = new Option<string>(new[] { "-o", "--output" },
                                       description: "File path for output JSON event dump")
{
    IsRequired = true
};

/// <summary>
/// 
/// </summary>

Option filterType = new Option<string>(new[] { "-ft", "--filtertype" }, 
                                       getDefaultValue: () => string.Empty, 
                                       description: "Filter type e.g. pid or proc"){};

/// <summary>
/// 
/// </summary>
Option filterValue = new Option<string>(new[] { "-fv", "--filtervalue" }, 
                                        getDefaultValue: () => string.Empty, 
                                        description: "Filter type e.g. pid or proc") {};

/// <summary>
/// 
/// </summary>
Option eventNamesFile = new Option<string>(new[] { "-en", "--eventnames" }, 
                                           getDefaultValue: () => string.Empty, 
                                           description: "File path for file containing event names e.g. to filter on"){};

RootCommand rc = new RootCommand { providersFile, outputFile, filterType, filterValue, eventNamesFile };
rc.Description = "Dumps ETW events system wide or filtered by PID";

/// <summary>
/// 
/// </summary>
rc.SetHandler((string providersFile, string outputFile, string filterType, string filterValue, string eventNamesFile) =>
{
    if (File.Exists(providersFile) == false)
    {
        Console.WriteLine($"Providers file does not exist");
        return;
    }

    if (eventNamesFile.Length > 0)
    {
        if (File.Exists(providersFile) == false)
        {
            Console.WriteLine($"Event keywords file does not exist");
            return;
        }
    }    

    EtwDumper etwDumper;
    switch (filterType)
    {
        case "":
            etwDumper = new EtwDumper(providersFile, outputFile, Global.FilterType.None, filterValue, eventNamesFile);
            break;

        case "pid":
            if (int.TryParse(filterValue, out int intOut))
            {
                etwDumper = new EtwDumper(providersFile, outputFile, Global.FilterType.Pid, intOut, eventNamesFile);
            }
            else 
            {
                Console.WriteLine($"Invalid PID filter value e.g. must be an integer");
                return;
            }
            
            break;

        case "proc":
            etwDumper = new EtwDumper(providersFile, outputFile, Global.FilterType.ProcessName, filterValue, eventNamesFile);
            break;

        default:
            Console.WriteLine($"Invalid filter type e.g. pid or proc");
            return;
    }

    if (etwDumper != null)
    {
        etwDumper.Dump();
    }

}, providersFile, outputFile, filterType, filterValue, eventNamesFile);

// Parse the incoming args and invoke the handler
return rc.Invoke(args);