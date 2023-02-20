using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Test1Data.LineProcess
{
    internal static class ProcessHelper
    {
        // I decided to extract this processing into separate unrelated class, so it would not constrict processors too much.
        public static (JsonArray result, int totalLines, int badLines) processLikeTxt(IEnumerable<string> lines)
        {
            int badLines = 0;
            int totalLines = 0;
            object _lock = new();


            JsonArray array = new(lines
                // I suppose, this allows parallel execution
                .AsParallel()
                // This uses that monstrosity of a method I wrote below to parse lines
                .Select(ParseLine)
                // Previous method returns null, if line is defective. Following filter returns only non-null entries and update badlines counter for all lines that returned null. This is not so elegant, but allowes continious execution, if needed.
                .Where(e =>
                {
                    lock (_lock)
                    {
                        totalLines++;
                        if (e is null)
                        {
                            badLines++;
                            return false;
                        }
                    }

                    return true;
                })
                // group by city.
                .GroupBy(e => e!.City)
                // for each city
                .Select(cityG =>
                {
                    // create city JSON object
                    var ct = new JsonObject()
                    {
                        ["city"] = cityG.Key,
                        ["services"] = new JsonArray(cityG
                        // group by service
                        .GroupBy(e => e!.Service)
                        // for ach service
                        .Select(serviceG =>
                        {
                            // create service JSON object
                            var serv = new JsonObject()
                            {
                                ["name"] = serviceG.Key,
                                ["payers"] = new JsonArray(serviceG
                                // for each person create JSON object
                                .Select(e => new JsonObject()
                                {
                                    ["name"] = e!.Name,
                                    ["payment"] = e.Payment,
                                    ["date"] = e.Date,
                                    ["account_number"] = e.AccountNumber,
                                })
                            .ToArray()),
                            };
                            // calculate total
                            serv["total"] = serv["payers"]!.AsArray().Sum(p => (decimal)(p!["payment"]!));
                            return serv;
                        })
                    .ToArray()),
                    };
                    // calculate total
                    ct["total"] = ct["services"]!.AsArray().Sum(p => (decimal)(p!["total"]!));
                    return ct;
                })
            // save to array, for creation of a JSON array
                .ToArray());

            return (array, totalLines, badLines);
        }

        private sealed class LineInfo
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            // No, I won't do that. This private class is used in a single place, and all of the field get initialies via "new(){ here }" thing.
            public string City { get; init; }
            public string Service { get; init; }
            public string Name { get; init; }
            public decimal Payment { get; init; }
            public long AccountNumber { get; init; }
            public string Date { get; init; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        }

        private static LineInfo? ParseLine(string line)
        {
            // expected format: <first_name: string>, <last_name: string>, <address: string>, <payment: decimal>, <date: date>, <account_number: long>, <service: string>
            // yeah, I'm going to manually parse this stuff, I guess it's gonna be even faster then any sort of framework

            // UPD: in fact, it's quite decent -- 10KB of data is getting processed in 100-200ms.

            try
            {
                // parse first_name
                int currentIndex = 0;
                int nextIndex = line.IndexOf(", ", currentIndex);
                if (nextIndex < 0)
                {
                    // no next comma -- string is invalid
                    return null;
                }
                string first_name = line[currentIndex..nextIndex];

                // parse last_surname
                currentIndex = nextIndex + 2;
                nextIndex = line.IndexOf(", ", currentIndex);
                if (nextIndex < 0)
                {
                    // no next comma -- string is invalid
                    return null;
                }
                string last_name = line[currentIndex..nextIndex];

                // parse address
                currentIndex = nextIndex + 2;
                nextIndex = line.IndexOf("“", currentIndex);
                if (nextIndex < 0)
                {
                    // no next *thing* -- string is invalid
                    return null;
                }
                currentIndex = nextIndex + 1;
                nextIndex = line.IndexOf("”", currentIndex);
                if (nextIndex < 0)
                {
                    // no next *thing* -- string is invalid
                    return null;
                }
                string address = line[currentIndex..nextIndex];
                // performing additional check on address, since it's gonna be parsed again
                int commaIndex = address.IndexOf(',');
                if (commaIndex <= 0)
                {
                    // comma is not present in the address, or is on the first place (meaning city name is empty) -- string is invalid
                    return null;
                }

                // parse payment
                currentIndex = nextIndex + 2;
                nextIndex = line.IndexOf(", ", currentIndex);
                if (nextIndex < 0)
                {
                    // no next comma -- string is invalid
                    return null;
                }
                decimal payment = decimal.Parse(line[currentIndex..nextIndex]);

                // parse date
                currentIndex = nextIndex + 2;
                nextIndex = line.IndexOf(", ", currentIndex);
                if (nextIndex < 0)
                {
                    // no next comma -- string is invalid
                    return null;
                }
                DateTime date = DateTime.ParseExact(line[currentIndex..nextIndex], "yyyy-dd-MM", CultureInfo.InvariantCulture);

                // parse account_number
                currentIndex = nextIndex + 2;
                nextIndex = line.IndexOf(", ", currentIndex);
                if (nextIndex < 0)
                {
                    // no next comma -- string is invalid
                    return null;
                }
                long accountNumber = long.Parse(line[currentIndex..nextIndex]);

                // parse service
                currentIndex = nextIndex + 2;
                if (currentIndex >= line.Length - 1)
                {
                    // no space for service -- string is invalid
                    return null;
                }
                string service = line[currentIndex..^0];

                return new LineInfo()
                {
                    City = address.Split(",")[0],
                    Service = service,
                    Name = $"{first_name} {last_name}",
                    Payment = payment,
                    Date = date.ToString("yyyy-dd-MM"),
                    AccountNumber = accountNumber,
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // here goes my attempt on using Regex-Match, but no luck with it so far.
        private static Regex lineFormat = new(@"$(?<FisrtName>[A-Za-z]+?),[ ]*(?<LastName>[A-Za-z]+?),[ ]*“(?<City>[A-Za-z]+?),.+?”,[ ]*(?<Payment>[0-9\.]+?),[ ]*(?<Date>[0-9-]+?),[ ]*(?<AccountNumber>\d+?),[ ]*(?<Service>[A-Za-z]+?)^");
        private static LineInfo? ParseLine2(string line)
        {
            // expected format: <first_name: string>, <last_name: string>, <address: string>, <payment: decimal>, <date: date>, <account_number: long>, <service: string>
            try
            {
                var m = lineFormat.Match(line);
                if (!m.Success)
                {
                    return null;
                }

                return new LineInfo()
                {
                    AccountNumber = long.Parse(m.Groups["AccountNumber"].Value),
                    City = m.Groups["City"].Value,
                    Date = DateTime.ParseExact(m.Groups["Date"].Value, "yyyy-dd-MM", CultureInfo.InvariantCulture).ToString("yyyy-dd-MM"),
                    Name = m.Groups["Name"].Value,
                    Payment = decimal.Parse(m.Groups["Payment"].Value),
                    Service = m.Groups["Service"].Value,
                };
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }
    }
}
