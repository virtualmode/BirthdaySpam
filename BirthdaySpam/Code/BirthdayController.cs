// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="BirthdayController.cs" company="Elcomplus LLC"></copyright>
// <date>2018-06-20</date>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam.Code
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;
    using System.Text;
    using System.Xml;

    //using NLog;

    public class BirthdayController
    {
        #region Constants

        private const string CULTURE_INFO_RU_RU = "ru-RU";
        private const string CULTURE_INFO_EN_US = "en-US";

        private const string BIRTHDAYS_PATH = "Birthdays/";
        private const string BIRTHDAYS_ARCHIVE_SUBPATH = "Archive/";
        private const string SETTINGS_FILE = "Settings.xml";
        private const string MEMBERS_FILE = "Members.xml";
        private const string PROPERTIES_FILE = "Properties.xml";
        private const string DEFAULT_FILE = "Views/index.html";
        private const string LANGUAGE_COOKIE = "language";
        private const string LANGUAGE_DEFAULT = CULTURE_INFO_RU_RU;
        private const string SESSION_COOKIE = "session";
        private const string MACROS_BIRTHDAY_BOYS_RU_RU = "BIRTHDAY_BOYS";
        private const string MACROS_RANDOM_HEX_COLOR = "RANDOM_HEX_COLOR";

        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int BIRTHDAY_WRITE_ATTEMPTS = 16;
        private const int BIRTHDAY_DEFAULT_FEE = 250;
        private const int BIRTHDAY_MIN_FEE = 100; // Limit to send mails.
        private const int MAIL_LIMIT = 29;

        #endregion Constants

        #region Fields

        private readonly object _lockObject;
        private readonly string[] _security;
        private readonly Random _random;
        //private readonly Logger _logger;
        private readonly HttpServer _server;
        private readonly XmlData _settings;
        private readonly XmlData _members;
        private readonly XmlData _properties;
        private readonly Dictionary<string, XmlData> _languages;

        public readonly int HttpPort;
        public readonly int HttpsPort;

        private int _loginAttempts;

        #endregion Fields

        #region Properties

        public string Address => $"http://localhost:{HttpPort}";

        #endregion Properties

        #region Constructors

        public BirthdayController()
        {
            _lockObject = new object();
            _security = new string[] { "..", ".xml", ".exe", ".dll", ".config", ".pdb" };
            _random = new Random();
            //_logger = LogManager.GetCurrentClassLogger();
            _settings = new XmlData();
            _members = new XmlData();
            _properties = new XmlData();
            _languages = new Dictionary<string, XmlData>();
            _loginAttempts = 0;
            LoadSettings();
            if (!int.TryParse(_settings.Keys[XmlData.HTTP_PORT][XmlData.VAL], out HttpPort)) HttpPort = 8089;
            if (!int.TryParse(_settings.Keys[XmlData.HTTPS_PORT][XmlData.VAL], out HttpsPort)) HttpsPort = 8443;
            _server = new HttpServer(HttpPort, HttpsPort);
            _server.OnHttpRequest += ServerOnHttpRequest;
        }

        #endregion Constructors

        #region Methods

        private string GenerateRandomString(int length)
        {
            string r = string.Empty;
            Random random = new Random();
            for (int i = 0; i < length; i++)
                r += $"{(char)('a' + random.Next() % ('z' - 'a' + 1))}";
            return r;
        }

        private string GenerateRandomHexColor()
        {
            return $"#{_random.Next(0x1000000):X6}";
        }

        /// <summary>
        /// Gets all combinations.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        static List<List<T>> GetCombinations<T>(List<T> list)
        {
            List<List<T>> result = new List<List<T>>();
            double count = Math.Pow(2, list.Count);
            for (int i = 1; i <= count - 1; i++)
            {
                List<T> current = new List<T>();
                string str = Convert.ToString(i, 2).PadLeft(list.Count, '0');
                for (int j = 0; j < str.Length; j++)
                {
                    if (str[j] == '1')
                    {
                        current.Add(list[j]);
                    }
                }
                result.Add(current);
            }
            return result;
        }

        /// <summary>
        /// Sends e-mail message.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        public void SendMessage(string from, List<BirthdayBoy> to, string subject, string body)
        {
            // Initialize SMTP:
            SmtpClient client = new SmtpClient(_settings.Keys[XmlData.SMTP_DOMAIN][XmlData.VAL])
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = true
                //UseDefaultCredentials = false,
                //client.Credentials = new NetworkCredential("IvanovII", "", "ELCOM")
            };

            // Send message:
            int i = 0, j;
            while (i < to.Count)
            {
                // Prepare message for group:
                MailMessage message = new MailMessage(from, to[i].Email)
                {
                    IsBodyHtml = true,
                    Subject = subject,
                    Body = body
                };
                for (j = i + 1; j - i < MAIL_LIMIT && j < to.Count; j++)
                {
                    message.To.Add(to[j].Email);
                }
                i = j;
                // Send message:
                client.Send(message);
            }
        }

        private void LoadSettings()
        {
            // Load settings and members:
            _settings.Load(SETTINGS_FILE);
            _members.Load(MEMBERS_FILE);
            _properties.Load(PROPERTIES_FILE);

            // Load all supported languages:
            string[] files = Directory.GetFiles("Content/", "*.xml", SearchOption.TopDirectoryOnly);
            _languages.Clear();
            foreach (var file in files)
            {
                XmlData language = new XmlData();
                language.Load(file);
                _languages.Add(Path.GetFileNameWithoutExtension(file) ?? file, language);
            }
        }

        public void Start()
        {
            LoadSettings();

            // Start new root session only if its value is empty:
            string session = _settings.Keys[SESSION_COOKIE][XmlData.VAL];
            if (string.IsNullOrWhiteSpace(session))
            {
                _settings.Keys[SESSION_COOKIE][XmlData.VAL] = GenerateRandomString(16);
                _settings.SyncXmlWithKeys();
                _settings.Save(SETTINGS_FILE);
            }

            // Start listener:
            _server.Start();
        }

        public void Stop()
        {
            _server.Stop();
            _settings.Save(SETTINGS_FILE);
            _members.Save(MEMBERS_FILE);
        }

        private Dictionary<string, string> GetArgs(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] args = query.Split(new char[] {'&', '?'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string arg in args)
            {
                string[] pair = arg.Split('=');
                if (!result.ContainsKey(pair[0]))
                    result.Add(pair[0], pair[1]);
            }
            return result;
        }

        private XmlData GetMember(int id)
        {
            XmlNode memberData = _members.FirstOrDefault(XmlData.ID, $"{id}");
            return memberData == null ? null : new XmlData(memberData);
        }

        /// <summary>
        /// Returns all birthdays.
        /// </summary>
        /// <param name="args">TODO: Not used.</param>
        /// <returns></returns>
        private XmlData GetBirthdays(Dictionary<string,string> args)
        {
            XmlData birthdays = new XmlData();
            DateTime today = DateTime.Now.Date; // Start of day.
            List<Dictionary<string, string>> orderedBirthdays = _members.Keys.Values
                .Select(e => new { Attribute = e, Birthday = DateTime.Parse(e[XmlData.BIRTHDAY]) })
                .Select(e => new { e.Attribute, Date = new DateTime(e.Birthday.Month >= today.Month ? today.Year : today.Year + 1, e.Birthday.Month, e.Birthday.Day) })
                .OrderBy(d => d.Date.Year)
                .ThenBy(d => d.Date.Month)
                .ThenBy(d => d.Date.Day)
                .Select(s => s.Attribute)
                .ToList();

            // Active and inactive members can participate in fees:
            foreach (Dictionary<string, string> attributes in orderedBirthdays)
            {
                var nodeAttributes = new Dictionary<string, string>();
                nodeAttributes.Add(XmlData.ID, attributes[XmlData.ID]);
                nodeAttributes.Add(XmlData.NAME, attributes[XmlData.NAME]);
                nodeAttributes.Add(XmlData.BIRTHDAY, attributes[XmlData.BIRTHDAY]);
                birthdays.Keys.Add(attributes[XmlData.ID], nodeAttributes);
            }
            birthdays.SyncXmlWithKeys();
            return birthdays;
        }

        /// <summary>
        /// Creates file with birthday.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private XmlData CreateBirthday(Dictionary<string,string> args)
        {
            if (args.ContainsKey(XmlData.ID) && int.TryParse(args[XmlData.ID], out int id) &&
                args.ContainsKey(XmlData.COMMENT))
            {
                // Find member for birthday record:
                XmlNode member = GetMember(id).FirstOrDefault(XmlData.ID, args[XmlData.ID]);
                if (member?.Attributes == null)
                    return null;

                // Fill members:
                XmlData birthday = new XmlData();
                foreach (Dictionary<string, string> attributes in _members.Keys.Values.OrderBy(n => n[XmlData.NAME]))
                {
                    var newAttributes = new Dictionary<string, string>(attributes);
                    newAttributes.Add(XmlData.FEE, "0"); // Add additional field.
                    birthday.Keys.Add(attributes[XmlData.ID], newAttributes);
                }
                birthday.SyncXmlWithKeys();
                // Fill specific attributes:
                birthday.DocumentElement.SetAttribute(XmlData.ID, member.Attributes[XmlData.ID].Value);
                birthday.DocumentElement.SetAttribute(XmlData.FEE, "0"); // Spent funds.
                birthday.DocumentElement.SetAttribute(XmlData.VAL, Uri.UnescapeDataString(args[XmlData.COMMENT])); // Comment.
                return birthday;
            }
            return null;
        }

        /// <summary>
        /// Gets current fee list.
        /// </summary>
        /// <param name="args">TODO: Not used.</param>
        /// <returns></returns>
        private XmlData GetFees(Dictionary<string,string> args)
        {
            XmlData fees = new XmlData();
            foreach (string fileName in Directory.GetFiles(BIRTHDAYS_PATH, "*.xml"))
            {
                int sum = 0, expected = 0;
                var fee = new XmlData();
                fee.Load(fileName);
                foreach (Dictionary<string, string> attributes in fee.Keys.Values)
                {
                    if (attributes[XmlData.ACTIVE] == "1")
                        expected += BIRTHDAY_DEFAULT_FEE;

                    if (int.TryParse(attributes[XmlData.FEE], out var i))
                        sum += i;
                }

                string id;
                if (fee.DocumentElement == null || (id = fee.DocumentElement.Attributes[XmlData.ID]?.Value) == null)
                    continue;

                XmlNode member = fee.FirstOrDefault(XmlData.ID, id);
                if (member?.Attributes == null)
                    continue;

                var feeAttributes = new Dictionary<string, string>();
                feeAttributes.Add(XmlData.ID, id); // Member id.
                feeAttributes.Add(XmlData.NAME, member.Attributes[XmlData.NAME].Value);
                feeAttributes.Add(XmlData.BIRTHDAY, member.Attributes[XmlData.BIRTHDAY].Value);
                feeAttributes.Add(XmlData.FILE, Path.GetFileName(fileName)); // File with fee data.
                feeAttributes.Add(XmlData.FEE, $"{sum}"); // Overall fee.
                feeAttributes.Add(XmlData.EXPECTED, $"{expected}"); // Expected fee.
                feeAttributes.Add(XmlData.VAL, fee.DocumentElement.Attributes[XmlData.VAL].Value); // Comment.
                fees.Keys.Add(fileName, feeAttributes);
            }
            fees.SyncXmlWithKeys();
            return fees;
        }

        /// <summary>
        /// Gets fee information.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private XmlData GetFee(Dictionary<string, string> args)
        {
            if (args.ContainsKey(XmlData.FILE) && !string.IsNullOrWhiteSpace(args[XmlData.FILE]))
            {
                string fileName = Path.Combine(BIRTHDAYS_PATH, Path.GetFileName(args[XmlData.FILE]));

                XmlData fee = new XmlData();
                fee.Load(fileName);

                return fee;
            }
            return null;
        }

        /// <summary>
        /// Gets sorted members.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private XmlData GetMembers(Dictionary<string,string> args)
        {
            XmlData members = new XmlData();
            List<Dictionary<string, string>> orderedBirthdays = _members.Keys.Values
                .Select(e => new { Attribute = e, Birthday = DateTime.Parse(e[XmlData.BIRTHDAY]) })
                .OrderBy(d => d.Birthday.Month)
                .ThenBy(d => d.Birthday.Day)
                .Select(s => s.Attribute)
                .ToList();

            foreach (Dictionary<string, string> attributes in orderedBirthdays)
            {
                members.Keys.Add(attributes[XmlData.ID], attributes);
            }
            members.SyncXmlWithKeys();
            return members;
        }

        /// <summary>
        /// Gets one of the properties.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private XmlData GetProperty(Dictionary<string, string> args)
        {
            XmlData property = new XmlData();
            if (args.ContainsKey(XmlData.ID) && !string.IsNullOrWhiteSpace(args[XmlData.ID]))
            {
                property.Keys[args[XmlData.ID]] = _properties.Keys[args[XmlData.ID]];
            }
            property.SyncXmlWithKeys();
            return property;
        }

        /// <summary>
        /// Sets one of the properties.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="inputStream"></param>
        /// <returns></returns>
        private void SetProperty(Dictionary<string, string> args, Stream inputStream)
        {
            if (inputStream != null)
            {
                XmlData properties = new XmlData();
                properties.Load(inputStream);
                foreach (var node in properties.Keys)
                {
                    _properties.Keys[node.Key][XmlData.VAL] = node.Value[XmlData.VAL];
                }
            }
            else if (args.ContainsKey(XmlData.ID) && !string.IsNullOrWhiteSpace(args[XmlData.ID]) &&
                args.ContainsKey(XmlData.VAL) && !string.IsNullOrWhiteSpace(args[XmlData.VAL]))
            {
                _properties.Keys[args[XmlData.ID]][XmlData.VAL] = Uri.UnescapeDataString(args[XmlData.VAL]);
            }
            _properties.SyncXmlWithKeys();
            _properties.Save(PROPERTIES_FILE);
        }

        /// <summary>
        /// Sends mail to all members.
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="debug"></param>
        private void SendMail(Stream inputStream, bool debug)
        {
            if (inputStream == null) // No input information.
                return;

            // Get birthday tree:
            string from = string.Empty;
            XmlData properties = new XmlData();
            List<BirthdayBoy> fees = new List<BirthdayBoy>();
            lock (_lockObject)
            {
                properties.Load(inputStream);
                from = properties.Keys[XmlData.MAIL_FROM][XmlData.VAL];
                // Fill birthday tree to simplify computing:
                foreach (string fileName in Directory.GetFiles(BIRTHDAYS_PATH, "*.xml"))
                {
                    var fee = new XmlData();
                    fee.Load(fileName);

                    if (fee.DocumentElement?.Attributes == null)
                        continue;

                    var birthdayFee = new BirthdayBoy
                    {
                        Id = fee.DocumentElement.Attributes[XmlData.ID].Value,
                        Val = fee.DocumentElement.Attributes[XmlData.VAL].Value // Comments.
                    };
                    fees.Add(birthdayFee);
                    foreach (Dictionary<string, string> attributes in fee.Keys.Values)
                    {
                        // Get attributes:
                        string id = attributes[XmlData.ID],
                            name = attributes[XmlData.NAME],
                            email = attributes[XmlData.EMAIL];

                        if (!DateTime.TryParse(attributes[XmlData.BIRTHDAY], out DateTime birthday))
                        {
                            birthday = DateTime.MinValue;
                            //_logger.Error(() => $"Incorrect birthday format for the '{name}'.");
                        }

                        if (!double.TryParse(attributes[XmlData.FEE], NumberStyles.None, CultureInfo.InvariantCulture, out double funds))
                            funds = 0;

                        // Save fee information:
                        if (birthdayFee.Id == id)
                        {
                            birthdayFee.Name = name;
                            birthdayFee.Email = email;
                            birthdayFee.Birthday = birthday;
                            birthdayFee.Fee = funds;
                            continue; // Not subscribe the birthday boy.
                        }

                        // Check minimal fee:
                        if (funds < BIRTHDAY_MIN_FEE && attributes[XmlData.ACTIVE] == "1" || // Compute mailing list only for active members.
                            email == from) // Send mail for admin everytime.
                        {
                            var birthdayBoy = new BirthdayBoy
                            {
                                Id = id,
                                Name = name,
                                Email = email,
                                Birthday = birthday,
                                Fee = funds
                            };

                            birthdayFee.Subscribers.Add(birthdayBoy);
                        }
                    }
                }
            }

            // Compute groups to send letters:
            CultureInfo cultureRuRu = CultureInfo.GetCultureInfo(CULTURE_INFO_RU_RU);
            StringBuilder debugMessage = new StringBuilder();
            var groups = GetCombinations(fees);
            List<BirthdayBoy> sent = new List<BirthdayBoy>();
            foreach (List<BirthdayBoy> boys in groups.OrderByDescending(b => b.Count))
            {
                IEnumerable<BirthdayBoy> mailingList = boys[0].Subscribers;
                for (int i = 1; i < boys.Count; i++)
                {
                    if (boys[i].Subscribers != null)
                        mailingList = boys[i].Subscribers.Intersect(mailingList);
                }

                // Generate messages:
                debugMessage.AppendLine("<div style='font-size:11px'>");
                string macrosBirthdayBoysRuRu = string.Empty;
                foreach (BirthdayBoy boy in boys)
                {
                    DateTime birthday = new DateTime(DateTime.Now.Year, boy.Birthday.Month, boy.Birthday.Day); // Birthday in this year.
                    string dayOfWeek = cultureRuRu.DateTimeFormat.GetDayName(birthday.DayOfWeek).ToLower();
                    macrosBirthdayBoysRuRu += $"{boy.Name} - {(string.IsNullOrEmpty(boy.Val) ? $"{birthday:d MMMM} ({dayOfWeek})" : $"{boy.Val}")}";
                    debugMessage.Append($"<b>{boy.Name} <a href='mailto:{boy.Email}'>{boy.Email}</a></b>");
                    if (boys.IndexOf(boy) < boys.Count - 1)
                    {
                        macrosBirthdayBoysRuRu += ",<br/>";
                        debugMessage.Append("; ");
                    }
                }
                debugMessage.AppendLine("<br/>");

                // Generate mailing list:
                List<BirthdayBoy> members = new List<BirthdayBoy>();
                foreach (BirthdayBoy boy in mailingList.ToList())
                {
                    if (!sent.Contains(boy))
                    {
                        debugMessage.AppendLine($"{boy.Name}<br/>");
                        sent.Add(boy); // List to filter.
                        members.Add(boy); // List to send.
                    }
                }
                debugMessage.AppendLine("<br/>");

                // Sending:
                if (!debug)
                {
                    SendMessage(from, members,
                        properties.Keys[XmlData.MAIL_SUBJECT][XmlData.VAL],
                        properties.Keys[XmlData.MAIL_MESSAGE][XmlData.VAL]
                            .Replace(MACROS_BIRTHDAY_BOYS_RU_RU, macrosBirthdayBoysRuRu)
                            .Replace(MACROS_RANDOM_HEX_COLOR, GenerateRandomHexColor()));
                }
                else if (members.Any(m => m.Email == from))
                {
                    SendMessage(from, new List<BirthdayBoy> {new BirthdayBoy { Email = from }},
                        properties.Keys[XmlData.MAIL_SUBJECT][XmlData.VAL],
                        properties.Keys[XmlData.MAIL_MESSAGE][XmlData.VAL]
                            .Replace(MACROS_BIRTHDAY_BOYS_RU_RU, macrosBirthdayBoysRuRu)
                            .Replace(MACROS_RANDOM_HEX_COLOR, GenerateRandomHexColor()));
                }
            }

            // For debug purposes:
            debugMessage.AppendLine("</div>");
            if (debug)
            {
                SendMessage(from, new List<BirthdayBoy> {new BirthdayBoy { Email = from }},
                    properties.Keys[XmlData.MAIL_SUBJECT][XmlData.VAL], $"{debugMessage}");
            }
        }

        /// <summary>
        /// Common HTTP requests handler.
        /// </summary>
        /// <param name="context"></param>
        private void ServerOnHttpRequest(HttpListenerContext context)
        {
            try
            {
                // Update cookies:
                Cookie languageCookie = context.Request.Cookies[LANGUAGE_COOKIE] ?? new Cookie(LANGUAGE_COOKIE, LANGUAGE_DEFAULT);
                Cookie sessionCookie = context.Request.Cookies[SESSION_COOKIE] ?? new Cookie(SESSION_COOKIE, string.Empty);
                languageCookie.Path = sessionCookie.Path = "/";
                context.Response.Cookies.Add(languageCookie);
                context.Response.Cookies.Add(sessionCookie);
                bool isLoggedIn = sessionCookie.Value == _settings.Keys[SESSION_COOKIE][XmlData.VAL];

                // Prevent to obtain system files:
                if (_security.Any(context.Request.Url.AbsolutePath.Contains))
                {
                    HttpServer.WriteFile(context, DEFAULT_FILE);
                    return;
                }

                // Request URL:
                string rawUrl = context.Request.Url.AbsolutePath.Substring(1); // Ignore first slash.

                // Determine file to serve:
                string fileName = string.IsNullOrEmpty(rawUrl) ? DEFAULT_FILE : rawUrl;
                if (File.Exists(fileName))
                {
                    HttpServer.WriteFile(context, fileName);
                    return;
                }

                // Custom command:
                int id; // Query id.
                XmlData member;
                Dictionary<string, string> args; // Query parameters.
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                // Administrative requests:
                if (isLoggedIn)
                {
                    switch (context.Request.Url.LocalPath)
                    {
                        case "/getSettings":
                            _settings.Save(context.Response.OutputStream);
                            break;

                        case "/setMember":
                            member = new XmlData();
                            member.Load(context.Request.InputStream);
                            args = GetArgs(context.Request.Url.Query);
                            if (args.ContainsKey(XmlData.ID) && int.TryParse(args[XmlData.ID], out id))
                                _members.ReplaceFirst(XmlData.ID, $"{id}", member);
                            else // New node:
                                _members.Add(member);

                            _members.Save(MEMBERS_FILE);
                            HttpServer.WriteFile(context, DEFAULT_FILE); // Change appropriate URL by JavaScript.
                            return;

                        case "/setProperty":
                            SetProperty(GetArgs(context.Request.Url.Query), context.Request.InputStream);
                            HttpServer.WriteFile(context, DEFAULT_FILE); // Change appropriate URL by JavaScript.
                            return;

                        case "/deleteMember":
                            args = GetArgs(context.Request.Url.Query);
                            if (args.ContainsKey(XmlData.ID) && int.TryParse(args[XmlData.ID], out id))
                            {
                                _members.DeleteFirst(XmlData.ID, $"{id}");
                                HttpServer.WriteFile(context, DEFAULT_FILE); // Change appropriate URL by JavaScript.
                                return;
                            }
                            break;

                        case "/addBirthday":
                            XmlData birthday = CreateBirthday(GetArgs(context.Request.Url.Query));
                            if (birthday != null)
                            {
                                int i = 1;
                                DateTime now = DateTime.Now;
                                string filePrefix = Path.Combine(BIRTHDAYS_PATH, $"{now.Year:00}{now.Month:00}{now.Day:00}");
                                while (i < BIRTHDAY_WRITE_ATTEMPTS)
                                {
                                    string fullName;
                                    if (!File.Exists(fullName = $"{filePrefix}{i:00}.xml"))
                                    {
                                        birthday.Save(fullName);
                                        break;
                                    }
                                    i++;
                                }
                            }
                            HttpServer.WriteFile(context, DEFAULT_FILE); // Change appropriate URL by JavaScript.
                            break;

                        case "/closeFee":
                            args = GetArgs(context.Request.Url.Query);
                            if (args.ContainsKey(XmlData.FILE) && !string.IsNullOrWhiteSpace(args[XmlData.FILE]))
                            {
                                string feeFileName = Path.GetFileName(args[XmlData.FILE].Trim()),
                                    archivePath = Path.Combine(BIRTHDAYS_PATH, BIRTHDAYS_ARCHIVE_SUBPATH);

                                if (!Directory.Exists(archivePath))
                                    Directory.CreateDirectory(archivePath);

                                File.Move(Path.Combine(BIRTHDAYS_PATH, feeFileName),
                                    Path.Combine(archivePath, feeFileName));

                                HttpServer.WriteFile(context, DEFAULT_FILE); // Change appropriate URL by JavaScript.
                                return;
                            }
                            break;

                        case "/getFee":
                            GetFee(GetArgs(context.Request.Url.Query)).Save(context.Response.OutputStream);
                            context.Response.OutputStream.Close();
                            return;

                        case "/saveFee":
                            lock (_lockObject)
                            {
                                args = GetArgs(context.Request.Url.Query);
                                if (args.ContainsKey(XmlData.FILE) && !string.IsNullOrWhiteSpace(args[XmlData.FILE]))
                                {
                                    XmlData fee = new XmlData(),
                                        feeDiff = new XmlData();
                                    string feeFileName = Path.Combine(BIRTHDAYS_PATH, args[XmlData.FILE]);
                                    fee.Load(feeFileName);
                                    feeDiff.Load(context.Request.InputStream);
                                    // Update fee:
                                    string feeId = fee.DocumentElement.Attributes[XmlData.ID].Value,
                                        feeFee = feeDiff.DocumentElement.Attributes[XmlData.FEE].Value,
                                        feeVal = feeDiff.DocumentElement.Attributes[XmlData.VAL].Value;
                                    foreach (var node in feeDiff.Keys)
                                    {
                                        fee.Keys[node.Key][XmlData.FEE] = node.Value[XmlData.FEE];
                                    }
                                    fee.SyncXmlWithKeys();
                                    // Fill specific attributes:
                                    fee.DocumentElement.SetAttribute(XmlData.ID, feeId);
                                    fee.DocumentElement.SetAttribute(XmlData.FEE, feeFee); // Spent funds.
                                    fee.DocumentElement.SetAttribute(XmlData.VAL, feeVal); // Comment.
                                    fee.Save(feeFileName);
                                }
                                context.Response.OutputStream.Close();
                            }
                            return;

                        case "/sendMail":
                            args = GetArgs(context.Request.Url.Query);
                            SendMail(context.Request.InputStream, args.ContainsKey(XmlData.DEBUG));
                            break;

                        default:
                            break;
                    }
                }

                // Guest requests:
                switch (context.Request.Url.LocalPath)
                {
                    case "/isLoggedIn":
                        context.Response.ContentType = "text/plain";
                        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                        {
                            writer.Write("{0}", isLoggedIn ? "1" : "0");
                        }
                        break;

                    case "/getSession": // TODO: Security lack!
                        if (_loginAttempts > MAX_LOGIN_ATTEMPTS)
                            break; // Prevent password brute force.

                        args = GetArgs(context.Request.Url.Query);
                        // Send session cookie to the client:
                        context.Response.ContentType = "text/plain";
                        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                        {
                            // Check unsecured login and password:
                            if (args.ContainsKey(XmlData.LOGIN) &&
                                args.ContainsKey(XmlData.PASSWORD) &&
                                !string.IsNullOrWhiteSpace(args[XmlData.LOGIN]) &&
                                !string.IsNullOrWhiteSpace(args[XmlData.PASSWORD]) &&
                                args[XmlData.LOGIN] == _settings.Keys[XmlData.LOGIN][XmlData.VAL] &&
                                args[XmlData.PASSWORD] == _settings.Keys[XmlData.PASSWORD][XmlData.VAL])
                            {
                                writer.Write("{0}", _settings.Keys[SESSION_COOKIE][XmlData.VAL]);
                            }
                            else
                            {
                                _loginAttempts++;
                            }
                        }
                        break;

                    case "/getLanguage":
                        _languages[languageCookie.Value]?.Save(context.Response.OutputStream);
                        break;

                    case "/getProperty":
                        GetProperty(GetArgs(context.Request.Url.Query)).Save(context.Response.OutputStream);
                        break;

                    case "/getMemberCount":
                        context.Response.ContentType = "text/plain";
                        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                        {
                            writer.Write("{0}", _members.Keys.Count);
                        }
                        break;

                    case "/getFeeCount":
                        context.Response.ContentType = "text/plain";
                        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                        {
                            writer.Write("{0}", Directory.GetFiles(BIRTHDAYS_PATH, "*.xml").Count().ToString());
                        }
                        break;

                    case "/getFees":
                        GetFees(GetArgs(context.Request.Url.Query)).Save(context.Response.OutputStream);
                        break;

                    case "/getMembers":
                        GetMembers(GetArgs(context.Request.Url.Query)).Save(context.Response.OutputStream);
                        break;

                    case "/getMember":
                        args = GetArgs(context.Request.Url.Query);
                        if (args.ContainsKey(XmlData.ID) && int.TryParse(args[XmlData.ID], out id))
                        {
                            member = GetMember(id);
                            if (member == null)
                            {
                                HttpServer.WriteFile(context, DEFAULT_FILE);
                                return;
                            }

                            member.Save(context.Response.OutputStream);
                        }
                        break;

                    case "/getBirthdays":
                        GetBirthdays(GetArgs(context.Request.Url.Query)).Save(context.Response.OutputStream);
                        break;

                    default: // JS will automatically show 404:
                        HttpServer.WriteFile(context, DEFAULT_FILE);
                        return;
                }

                context.Response.OutputStream.Close();
            }
            catch
            {
                //_logger.Error(() => $"On HTTP request error: {ex.Message}");
            }
        }

        #endregion Methods
    }
}
