using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DotNetWikiBot;

namespace AssessmentDotNet
{
	class Program : Bot
	{
		public static List<string> ImportanceCategories = new List<string> {
			"Besorolatlan",
			"Kevéssé fontos",
			"Közepesen fontos",
			"Nagyon fontos",
			"Nélkülözhetetlen"
		};
		public static List<string> QualityCategories = new List<string> {
			"Besorolatlan",
			"Születő besorolású",
			"Vázlatos besorolású",
			"Bővítendő besorolású",
			"Jól használható besorolású",
			"Teljes besorolású",
			"Színvonalas besorolású",
			"Kitüntetett besorolású"
		};

		const int botMainVersion = 2;
		const int botSubVersion = 14;
		const string specialVersionSign = "";
		const string RootCategory = "Kategória:Wikipédia-cikkértékelés";
		const string QualityCat = "Kategória:{0} szócikkek minőség szerint";
		const string ImportanceCat = "Kategória:{0} szócikkek fontosság szerint";
		const string DefaultCat = "Kategória:{1} {0} szócikkek";
		const string QualityCatRegex = @"Kategória:(.*?) szócikkek minőség szerint";
		const string TalkPageRegex = @"^Vita:(?<page>.*)";
		const string WPAssessment = "Wikipédia:Cikkértékelési műhely";
		const string WPAssessmentCore = "Wikipédia:Cikkértékelési műhely/{0} szócikkek";
		const string StatisticsPage = "/Statisztika";
		const string LogPage = "/Napló";
		const string SubpageRegex = @"\[\[(?<page>Wikipédia:Cikkértékelési műhely\/{0} szócikkek\/\d+)\|\d+. oldal\]\]";
		const string AssessmentRegex = @"{{ *Cikkértékelés\/Értékelés *\| *szócikk *= *(?<szócikk>.*?) *\| *oldid *= *(?<oldid>\d+) *\| *dátum *= *(?<dátum>\d{4}-\d{2}-\d{2}) *\| *besorolás *= *(?<besorolás>\d+) *\| *fontosság *= *(?<fontosság>\d+) *}}";
		const string CapitalStartRegex = @"nagybetűs[ ]*=[ ]*(?<capital>.*?)[ ]*\n";
		const string ArticleLogLink = "[[{0}]] <small>([[Vita:{0}|vita]])</small>";
		const string IndexRegex = @"(?<pre>.*\<!-- téma --\>).*?(?<inner>\<!-- !téma --\>.*?\<!-- lista --\>\n).*?(?<after>\<!-- !lista --\>.*)";
		const string BotLink = "[[Szerkesztő:{0}|{0}]]";
		const string WikiDateFormat = "yyyy-MM-dd";
		const string NormalDateFormat = "[[yyyy]]. [[MMMM dd.|MMMM dd]]-i";


		static Dictionary<string, AssessmentCategoryInfo> categories;
		static int[,] allCounter;
		static Dictionary<string, int> artCounter;
		static List<string> updateList;

		static Site huwiki;

		static string GetVersion()
		{
			return string.Format("{0}.{1:00}{2}", botMainVersion, botSubVersion, specialVersionSign);
		}

		static void Main(string[] args)
		{
			bool forceSave = false;
			
			Console.WriteLine("Wikipédia cikkértékelés v{0}, készítette: Tar Dániel, 2008-2010", GetVersion());
			Console.WriteLine("================================================");

			// Autentikációs adatok beolvasása
			var loginDataPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "Cache", "Defaults.dat");
			string[] loginContents = null;
			try {
				loginContents = File.ReadAllText(loginDataPath)
					.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			}
			catch (Exception ex) {
				Console.WriteLine("Nem sikerült beolvasni a bejelentkezési adatokat.");
			}

			if (loginContents == null || loginContents.Length < 3)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Hiba: hiányzik a Cache/Defaults.dat fájl");
				Console.ReadKey();
				return;
			}

			if (loginContents[0].StartsWith("https://") == false)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Hiba: a Cache/Defaults.dat fájlban a wikipédia elérési útja nem HTTPS-sel kezdődik\n  (pl. https://hu.wikipedia.org/ )");
				Console.ReadKey();
				return;
			}


			// Inicializáció
			try
			{
				huwiki = new Site(loginContents[0], loginContents[1], loginContents[2]);
				Console.WriteLine("Sikeresen bejelentkeztél " + huwiki.userName + " néven.");
			}
			catch (WebException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Valami probléma van az internetkapcsolatoddal. A bot működése nem folytatható");
				Console.ReadKey();
				Environment.Exit(0);
			}
			catch (WikiBotException e)
			{
				Console.WriteLine("Hiba lépett fel: " + e.Message + " A bot működése nem folytatható.");
				Console.ReadKey();
				Environment.Exit(0);
			}

			try
			{
				bool programEnabled = false;
				updateList = new List<string>();
				categories = new Dictionary<string, AssessmentCategoryInfo>();
				allCounter = new int[QualityCategories.Count, ImportanceCategories.Count];
				artCounter = new Dictionary<string, int>();

				// Botjogok feldolgozása
				GetBotFlags(ref programEnabled);

				if ((updateList.Contains("*") || updateList.Contains("initCat")) &&
					args.Length > 0 && args[0].StartsWith("-init:"))
				{
					string[] quals = {"Besorolatlan", "Születő besorolású", "Vázlatos besorolású",
						"Bővítendő besorolású", "Jól használható besorolású", "Teljes besorolású",
						"Színvonalas besorolású", "Kitüntetett besorolású"};
					string[] imps = {"Kevéssé fontos", "Közepesen fontos",
						"Nagyon fontos", "Nélkülözhetetlen"};
					string xCat = args[0].Substring(6);

					Console.WriteLine(xCat + " szócikkek kategóriáinak létrehozása...");

					Page xMainCat = new Page(huwiki, "Kategória:" + xCat + " szócikkek minőség szerint");
					xMainCat.Save("[[Kategória:Wikipédia-cikkértékelés]]", "Bot: Cikkértékelési kategória létrehozása", true);
					Page xImpCat = new Page(huwiki, "Kategória:" + xCat + " szócikkek fontosság szerint");
					xImpCat.Save("[[Kategória:Wikipédia-cikkértékelés]]", "Bot: Cikkértékelési kategória létrehozása", true);
					for (int i = 0; i < 8; i++)
					{
						Page xQualSubCat = new Page(huwiki, "Kategória:" + quals[i] + " " + xCat + " szócikkek");
						xQualSubCat.Save("[[Kategória:" + xCat + " szócikkek minőség szerint|" + i.ToString() + "]]",
							"Bot: Cikkértékelési kategória létrehozása", true);
					}
					for (int i = 1; i <= 4; i++)
					{
						Page xImpSubCat = new Page(huwiki, "Kategória:" + imps[i - 1] + " " + xCat + " szócikkek");
						xImpSubCat.Save("[[Kategória:" + xCat + " szócikkek fontosság szerint|" + i.ToString() + "]]",
							"Bot: Cikkértékelési kategória létrehozása", true);
					}
					Page dataPage = new Page("Wikipédia:Cikkértékelési műhely/" + xCat.ToUpperFirst() + " szócikkek/Beállítások");
					dataPage.Save(@"{{#switch:{{{1}}}
| ikon = Antialias Icon.svg
| műhely = 
| portál = 
| nagybetűs = 
<!-- E FÖLÉ ÍRD AZ ADATOKAT, ALATTA NE MÓDOSÍTS -->
| {{{1}}} = {{Cikkértékelés/Beállítások}}
}}",
						"Bot: Cikkértékelési kategória létrehozása", true);

					updateList = new List<string>()
					{
						xCat.ToUpperFirst()
					};

					//Console.ReadKey();
					//return;
				}
				else if (args.Length > 0 && args[0].StartsWith("-init:"))
				{
					Console.WriteLine("Nincs jogod kategóriákat létrehozni.");
					Console.ReadKey();
					return;
				}
				else if (args.Length > 0 && args[0] == "-forcesave")
				{
					forceSave = true;
				}

				// Cikkértékelési kategóriák betöltése
				LoadCategories();

				// Botadatok betöltése, következő mentés előkészítése
				XmlDocument savedBotData = new XmlDocument();
				XmlDocument botData = new XmlDocument();
				XmlElement botDRoot = botData.CreateElement("Cikkértékelés");
				
				if (!updateList.Contains("*"))
				{
					Console.WriteLine("Korábban elmentett adatok betöltése...");
					string bf = WebHelpers.GetUrlContent("https://hu.wikipedia.org/w/index.php?title=Wikip%E9dia:Cikk%E9rt%E9kel%E9si_m%FBhely/Botadatok&action=raw&ctype=text/x-wiki");
					savedBotData.LoadXml(bf);
				}

				foreach (string key in categories.Keys)
				{

					// Megnézzük, hogy a botnak van-e jogosultsága frissíteni a témakört.
					// Ha nem, akkor az előző bot által mentett adatokat használjuk fel (ha vannak, és nem új a témakör)
					if (!updateList.Contains("*") && !updateList.Contains(key))
					//if (key!="Első világháborús témájú")
					{
						// TODO: adat kód
						int allArt = 0, assArt = 0;
						XmlElement oldCatRoot = CopyOldData(key, savedBotData, botData, ref allArt, ref assArt);
						if (oldCatRoot != null) // A kategória még nem lett feldolgozva, mert üres vagy új
						{                       // A lekorlátozott jogú bot ekkor semmit sem tud tenni
							botDRoot.AppendChild(oldCatRoot);
							categories[key].AllArticles = allArt;
							categories[key].AssessedArticles = assArt;
						}
						continue;
					}

					Console.WriteLine("\n\n== " + key + " szócikkek ==\n");

					// Beállítások oldal letöltése
					// Jelenleg csak arra vagyunk innen kíváncsiak, hogy nagybetűvel kezdődik-e a témakör neve
					Console.Write("Beállítások letöltése... ");
					Page optionsPage = new Page(huwiki, "Wikipédia:Cikkértékelési műhely/" + key + " szócikkek/Beállítások");
					optionsPage.Load();
					if (optionsPage.Exists())
					{
						Console.WriteLine(" kész.");
						string capt = Regex.Match(optionsPage.text, CapitalStartRegex).Groups["capital"].Value;
						categories[key].CapitalStart = (capt == "true" || capt == "igen" || capt == "yes") ? true : false;
					}
					else
					{
						Console.WriteLine(" az oldal nem létezik.");
					}

					// Egy kis inicializáció.
					// oldArts: szócikkek listája a Wikipédián megtalálható lapokról, kiindulási alapnak
					// newArts: a kategóriákból begyűjtött szócikklista
					// counter: az egyes értékelési párokhoz (besorolás, fontosság) tartozó szócikkek száma
					Dictionary<string, Article> oldArts = new Dictionary<string, Article>();
					Dictionary<string, Article> newArts = new Dictionary<string, Article>();
					int[,] counter = new int[QualityCategories.Count, ImportanceCategories.Count];

					// oldArts feltöltése a Wikipédiára elmentett lapokból
					// - vagy - ha nem létezik a főlap (Wikipédia:Cikkértékelési műhely/{0} szócikkek)
					//          akkor megjegyezzük, hogy új kategóriát találtunk

					Page catMain = new Page(huwiki, string.Format(WPAssessmentCore, key));
					catMain.Load();
					if (catMain.Exists())
					{
						LoadOldAssessment(key, ref oldArts, ref catMain);
					}
					else
					{
						categories[key].NewCat = true;
					}

					// Begyűjtjük a szócikkek jelenlegi besorolását és fontosságát, oldid-t nem
					CollectArticlesFromCategories((categories[key].CapitalStart ? key : key.ToLower()), newArts);

					// Az oldArts és a newArts közötti különbségek megtekintése
					// és az érvénytelen szócikkek (nincs laptörténetük) invalidnak jelölése
					// Futtatási napló elkészítése
					int diffs = 0;
					string log = Diff(oldArts, ref newArts, out diffs);

					// Ha a szócikkek száma nulla, nem mentjük el a semmit sem tartalmazó listát, hanem a következő témakörre ugrunk
					if (newArts.Count == 0)
					{
						Console.WriteLine("A kategória egyetlen szócikket sem tartalmaz, ezért nem kerül feldolgozásra.");
						continue;
					}

					// Eltároljuk az összes szócikk számát, valamint a besorolt szócikkek számát, egyúttal megszámoljuk,
					// hogy az egyes (besorolás, fontosság) párokhoz mennyi szócikk tartozik
					categories[key].AllArticles = newArts.Count;
					categories[key].AssessedArticles = CountArticles(newArts, counter);

					// Változtatások elmentése, ha van változás
					//                             - vagy - kényszerített mentés van

					if (forceSave || diffs > 0)
					{
						// Statisztika elmentése
						Console.WriteLine("Statisztika elmentése ...");
						Page catStat = new Page(huwiki, string.Format(WPAssessmentCore, key) + StatisticsPage);
						//catStat.Load();
						catStat.Save(PrintStat(counter, key), "Bot: statisztika " + DateTime.Today.ToString("yyyy-MM-dd") + "-i frissítése", false);

						// Szócikklisták és index elmentése
						if (newArts.Keys.Count > 80)
						{
							string index = "{{Sablon:Cikkértékelés/Fejléc|téma=" + key + "}}\n" +
								"<div style=\"float:right\">{{Wikipédia:Cikkértékelési műhely/" + key + " szócikkek/Statisztika}}</div>\n" +
								PrintQualityList(newArts, key) +
								"\n{{Cikkértékelés/Indexlábléc | összesen = " + newArts.Count.ToString() + "}}";

							Page botSavePage = new Page(huwiki, "Wikipédia:Cikkértékelési műhely/" + key + " szócikkek/Bot");
							botSavePage.Save(
								"{{#switch:{{{1|}}}\n |bot=" + huwiki.userName + "\n |dátum=" + DateTime.Today.ToString("yyyy-MM-dd") + "\n}}",
								"Bot: lap frissítése",
								false);


							Console.WriteLine("A témakör indexének elmentése ...");
							Page assessmentIndex = new Page(huwiki, string.Format(WPAssessmentCore, key));
							assessmentIndex.Save(index, "Bot: témakör indexének frissítése", false);
						}
						else
						{
							Console.WriteLine("Szócikklista elmentése ...");
							PrintQualityList(newArts, key);
						}
					}

					// Napló elmentése, ha nem új a cikkértékelési kategória
					if (categories[key].NewCat != true)
					{
						Console.WriteLine("Napló elmentése ...");
						Page logPage = new Page(huwiki, string.Format(WPAssessmentCore + LogPage, key));
						logPage.Save(log, "Bot: futtatás naplózása", false);
					}

					// Adatok elmentése XML-be
					XmlElement catRoot = botData.CreateElement("Témakör");
					XmlAttribute catName = botData.CreateAttribute("név");
					catName.Value = key;
					catRoot.Attributes.Append(catName);
					for (int i = 0; i < QualityCategories.Count; i++)
					{
						for (int j = 0; j < ImportanceCategories.Count; j++)
						{
							XmlAttribute curVal = botData.CreateAttribute("d" + i.ToString() + "_" + j.ToString());
							curVal.Value = counter[i, j].ToString();
							catRoot.Attributes.Append(curVal);
						}
					}
					botDRoot.AppendChild(catRoot);

				}

				// Összesített statisztika elmentése
				Console.WriteLine("Összesített statisztika elmentése ...");
				Page allStat = new Page(huwiki, WPAssessment + StatisticsPage);
				allStat.Save(PrintStat(allCounter, ""), "Bot: összesített statisztika " + DateTime.Today.ToString("yyyy-MM-dd") + "-i frissítése", false);

				// Index elmentése
				Console.WriteLine("Cikkértékelés indexének elmentése ...");
				Page p = new Page(huwiki, "Wikipédia:Cikkértékelési műhely/Index");
				p.Load();
				Match m = Regex.Match(p.text, IndexRegex, RegexOptions.Singleline);
				string list = "";
				int validCats = 0;
				foreach (string s in categories.Keys)
				{
					if (categories[s].AllArticles > 0)
					{
						validCats++;
						list += "{{Cikkértékelés/Index | téma = " + s + " | ellenőrzött = " + categories[s].AssessedArticles + " | összesen = " + categories[s].AllArticles + "}}\n";
					}
				}
				p.Save(m.Groups["pre"].Value + validCats + m.Groups["inner"].Value + list + m.Groups["after"].Value, "Bot: index frissítése", false);

				// TöbbWP elintézése
				//if (updateList.Contains("*"))
				//{
				//	PageList moreWPs = new PageList(huwiki);
				//	List<string> moreWPList = new List<string>();
				//	moreWPs.FillFromCustomApiQuery("embeddedin", "eititle=Sablon:Több_WP&eilimit=5000", int.MaxValue);
				//	//moreWPs.FillFromCustomBotQueryList("embeddedin", "eititle=Sablon:Több_WP&eilimit=5000", int.MaxValue);
				//	// átrakjuk a lapok címeit egy listába, mert a Page-k közötti keresgélés kurva lassú
				//	foreach (Page px in moreWPs)
				//	{
				//		moreWPList.Add(px.title);
				//	}
				//	string log = "==TöbbWP sablon nélküli, de több cikkértékelési kategóriába tartozó sablonok==\n";
				//	foreach (string s in artCounter.Keys)
				//	{
				//		if (artCounter[s] > 1 && !moreWPList.Contains("Vita:" + s))
				//			log += "* " + string.Format(ArticleLogLink, s) + " (" + artCounter[s] + " témakörben)\n";
				//	}
				//	Page mwLog = new Page(huwiki, "Wikipédia:Cikkértékelési műhely/Több WP");
				//	mwLog.Save(log, "Bot: Több WP-napló frissítése", false);
				//}

				// Botadatok elmentése
				Console.WriteLine("Adatok elmentése ...");
				botData.AppendChild(botDRoot);
				Page botDataP = new Page(huwiki, "Wikipédia:Cikkértékelési műhely/Botadatok");
				botDataP.Save(botData.XmlToString(), "Bot: botadatok elmentése", false);
			}
			catch (Exception e)
			{
				Console.WriteLine("Hiba történt: ");
				Console.WriteLine(e.Message);
				Console.WriteLine();
				Console.WriteLine("Stack trace:");
				Console.WriteLine(e.StackTrace);
				Console.ReadKey();
			}
			//Console.ReadLine();
		}

		static void GetBotFlags(ref bool programEnabled)
		{
			XmlDocument botFlags = new XmlDocument();
			string bf = WebHelpers.GetUrlContent("https://hu.wikipedia.org/w/index.php?title=Wikip%E9dia:Cikk%E9rt%E9kel%E9si_m%FBhely/Botjogok&action=raw&ctype=text/x-wiki");
			botFlags.LoadXml(bf);

			if (botFlags.DocumentElement.HasAttribute("programVerzió") &&
				GetVersion() == botFlags.DocumentElement.Attributes["programVerzió"].Value)
			{
				foreach (XmlNode n in botFlags.DocumentElement.ChildNodes)
				{
					if (n is XmlElement)
					{
						XmlElement e = n as XmlElement;
						if (e.Attributes["név"].Value == huwiki.userName)
						{
							programEnabled = true;
							if (e.Attributes["jogok"].Value != "*")
							{
								string consMess = "A bot számára a következő cikkértékelési kategóriák frissítése engedélyezett:\n";
								int i = 1;
								foreach (string s in e.Attributes["jogok"].Value.Split(','))
								{
									if (s != "initCat")
									{
										consMess += "  " + i + ". " + s + "\n";
									}
									else
									{
										consMess += "  + új cikkértékelési kategória bevezetése";
									}
									updateList.Add(s);
									i++;
								}
								Console.WriteLine(consMess);
							}
							else
							{
								updateList.Add("*");
								Console.WriteLine("A bot számára az összes cikkértékelési kategória frissítése engedélyezve van.");
							}
						}
					}
				}

				if (!programEnabled)
				{
					Console.WriteLine("A botod számára nincs engedélyezve a cikkértékelő proram futtatása.\n" +
									  "Ha futtatni szeretnéd, kérj engedélyt Szerkesztő:Danitól, vagy a Cikkértékelési műhely vitalapján");
					Console.ReadKey();
					Environment.Exit(0);
				}
			}
			else
			{
				Console.WriteLine("A cikkértékelő programnak új verziója érhető el (" + botFlags.DocumentElement.Attributes["programVerzió"].Value + ").\n" +
								  "Ha továbbra is futtatni szeretnéd a botot, szerezd be az új változatot!");
				Console.ReadKey();
				Environment.Exit(0);
			}
		}
		static void LoadCategories()
		{
			// Értékelési kategóriák betöltése
			PageList cats = new PageList(huwiki);
			cats.FillAllFromCategory(RootCategory);
			cats.FilterNamespaces(new int[] { 14 });

			// A Kategória:Wikipédia-cikkértékelés alkategóriáinak végigvizsgálása
			foreach (Page p in cats.pages)
			{
				if (Regex.IsMatch(p.title, QualityCatRegex)) // Új cikkértékelési kategória megtalálva
				{
					string cat = Regex.Match(p.title, QualityCatRegex).Groups[1].Value;

					categories.Add(cat, new AssessmentCategoryInfo()
					{
						HasImportance = cats.Contains(string.Format(ImportanceCat, cat)), // Van-e fontosság szerinti értékelési kategória
						NewCat = false,
						CapitalStart = false
					});
				}
			}
		}
		static XmlElement CopyOldData(string key, XmlDocument oldData, XmlDocument newData, ref int allArt, ref int assArt)
		{
			foreach (XmlNode node in oldData.DocumentElement.ChildNodes)
			{
				if (node is XmlElement)
				{
					XmlElement elem = node as XmlElement;
					//Console.WriteLine(elem.Attributes["név"].Value + "..." + key);
					if (elem.HasAttribute("név") && elem.Attributes["név"].Value == key)
					{

						XmlElement ret = newData.CreateElement("Témakör");
						XmlAttribute catName = newData.CreateAttribute("név");
						catName.Value = key;
						ret.Attributes.Append(catName);
						for (int i = 0; i < QualityCategories.Count; i++)
						{
							for (int j = 0; j < ImportanceCategories.Count; j++)
							{
								XmlAttribute curVal = newData.CreateAttribute("d" + i.ToString() + "_" + j.ToString());
								curVal.Value = elem.Attributes["d" + i.ToString() + "_" + j.ToString()].Value;
								ret.Attributes.Append(curVal);
								allCounter[i, j] += Convert.ToInt32(elem.Attributes["d" + i.ToString() + "_" + j.ToString()].Value);
								allArt += Convert.ToInt32(elem.Attributes["d" + i.ToString() + "_" + j.ToString()].Value);
								if (i != 0)
								{
									assArt += Convert.ToInt32(elem.Attributes["d" + i.ToString() + "_" + j.ToString()].Value);
								}
							}
						}
						return ret;
					}
				}
			}
			return null;
		}
		static void LoadOldAssessment(string key, ref Dictionary<string, Article> arts, ref Page catMain)
		{
			// Megnézzük, hogy a cikkértékelő témakörhöz tartozó lapon vannak-e hivatkozások /{key}. oldal
			// nevű allapra. Ha igen, akkor minden egyes allapról betöltjük a szócikkek adatait,
			// ha nem, akkor csak a főlapról (ekkor van 1 oldalunk)
			MatchCollection matches = Regex.Matches(catMain.text, string.Format(SubpageRegex, key));
			if (matches.Count > 0) // Aloldalakra van bontva
			{
				foreach (Match m in matches)
				{
					ExtractOldAssessment(m.Groups["page"].Value, ref arts);
				}
			}
			else // Még csak egyetlen oldal van
			{
				ExtractOldAssessment(catMain.title, ref arts);
			}
		}
		static void ExtractOldAssessment(string page, ref Dictionary<string, Article> arts)
		{
			// Betöltjük az adott allapot (Wikipédia:Cikkértékelési műhely/{key} szócikkek/{page}. oldal
			//                              - vagy - Wikipédia:Cikkértékelési műhely/{key} szócikkek )
			// és elmentjük a szócikk adatait (név, oldid, dátum, besorolás, fontosság)
			Console.Write("Korábbi szócikklista betöltése: " + page + "\n  ...  ");
			Page extr = new Page(huwiki, page);
			extr.Load();
			if (extr.Exists())
			{
				MatchCollection matches = Regex.Matches(extr.text, AssessmentRegex);
				int count = 0;
				foreach (Match m in matches)
				{
					if (!arts.ContainsKey(m.Groups["szócikk"].Value))
					{
						arts.Add(m.Groups["szócikk"].Value, new Article()
						{
							Name = m.Groups["szócikk"].Value,
							Date = DateTime.ParseExact(m.Groups["dátum"].Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
							Importance = new Importance(Convert.ToInt32(m.Groups["fontosság"].Value)),
							Quality = new Quality(Convert.ToInt32(m.Groups["besorolás"].Value)),
							OldId = m.Groups["oldid"].Value
						});
						count++;
					}
					else
					{
						Console.WriteLine("A(z) {0} kétszer található meg, valószínűleg korábbi futás során történt leállás miatt", m.Groups["szócikk"].Value);
					}
				}
				Console.WriteLine("sikerült, {0} szócikk adatait eltároltam", count);
			}
			else
			{
				Console.WriteLine("a lap nem létezik");
			}
		}

		static void CollectArticlesFromCategories(string key, Dictionary<string, Article> arts)
		{
			// Először végigmengyünk a besorolási kategóriákon, és betöltjük belőlük a szócikkeket,
			// eltároljuk őket a Dictionary-ben a jelenlegi dátummal és a megfelelő besorolással,
			// fontosság nélkül.
			Console.WriteLine("\nBesorolási kategóriák vizsgálata ...");
			foreach (string s in QualityCategories)
			{
				PageList p = new PageList(huwiki);

				p.FillFromCategory(string.Format(DefaultCat, key, s));
				Console.WriteLine(" : " + s + ": " + (p.Count() > 0 ? string.Format("{0} szócikket tartalmaz", p.Count()) : "nem tartalmaz szócikkeket"));
				foreach (Page art in p)
				{
					if (Regex.IsMatch(art.title, TalkPageRegex))
					{
						string pageName = Regex.Match(art.title, TalkPageRegex).Groups["page"].Value;
						try
						{
							arts.Add(pageName, new Article()
							{
								Name = pageName,
								Date = DateTime.Today,
								Importance = null,
								Quality = new Quality(QualityCategories.IndexOf(s)),
								OldId = ""
							});
						}
						catch
						{
						}
					}
				}
			}

			// Másodszor a fontossági kategóriákon megyünk végig (kihagyva az elsőt), és ha már volt
			// besorolás szerint értékelve az adott szócikk, akkor beállítjuk a fontosságot.
			// Ha nem volt, akkor figyelmen kívül hagyjuk, mert csak fontosság szerint nem értékelünk.
			Console.WriteLine("\nFontossági kategóriák vizsgálata ...");
			bool skipped = false;
			foreach (string s in ImportanceCategories)
			{
				if (!skipped)
				{
					// kihagyjuk az első kategóriát, aszerint nincs fontossági értékelés
					skipped = true;
					continue;
				}

				PageList p = new PageList(huwiki);
				p.FillFromCategory(string.Format(DefaultCat, key, s));
				Console.WriteLine(" : " + s + ": " + (p.Count() > 0 ? string.Format("{0} szócikket tartalmaz", p.Count()) : "nem tartalmaz szócikkeket"));
				foreach (Page art in p)
				{
					if (Regex.IsMatch(art.title, TalkPageRegex))
					{
						string pageName = Regex.Match(art.title, TalkPageRegex).Groups["page"].Value;
						if (arts.ContainsKey(pageName))
						{ // csak akkor állítunk fontossági szintet, ha minőségi kategóriákban megtaláltuk a szócikket
							arts[pageName].Importance = new Importance(ImportanceCategories.IndexOf(s));
						}
					}
				}
			}
		}
		static int CountArticles(Dictionary<string, Article> arts, int[,] counter)
		{
			int assessed = 0;
			foreach (Article art in arts.Values)
			{
				int imp = art.Importance == null ? 0 : art.Importance.Value;
				counter[art.Quality.Value, imp]++;
				allCounter[art.Quality.Value, imp]++;
				if (artCounter.ContainsKey(art.Name))
				{
					artCounter[art.Name]++;
				}
				else
				{
					artCounter.Add(art.Name, 1);
				}
				if (art.Quality.Value > 0) assessed++;
			}
			return assessed;
		}

		static string LastRev(string title)
		{
			// Lekérjük a lap legutolsó változatának azonosítóját
			Console.Write(title + " legutolsó változata azonosítójának lekérése ... ");
			PageList lastRev = new PageList(huwiki);
			lastRev.FillFromPageHistory(title, 1);
			if (lastRev.Count() > 0)
			{
				Console.WriteLine(" sikerült (" + lastRev[0].revision + ")");
				return lastRev[0].revision;
			}
			Console.WriteLine(" sikertelen: a szócikk nem létezik");
			return "";
		}

		static string Diff(Dictionary<string, Article> oldArts, ref Dictionary<string, Article> newArts, out int diffs)
		{
			diffs = 0;
			string log = "== {{#time:Y. F j.|" + DateTime.Today.ToString("yyyy-MM-dd") + "}}, " + string.Format(BotLink, huwiki.userName) + " ==\n";

			Dictionary<string, Article> retArts = new Dictionary<string, Article>();

			// Végigmegyünk az új szócikkeket tartalmazó tömb listáján, hogy:
			// 1) megnézzük, van-e új
			// 2) megnézzük az értékelésben történt változásokat
			// 3) kiszűrjük az érvénytelen szócikkeket
			foreach (KeyValuePair<string, Article> kw in newArts)
			{
				string s = kw.Key;
				Article newArt = kw.Value;

				// Új szócikkel van dolgunk
				if (!oldArts.ContainsKey(s))
				{
					// Megnézzük, hogy a szócikk valóban létezik-e (van oldidje),
					// ha igen, akkor elmentjük a jelenlegi dátumot, és naplózzuk, hogy új szócikk van
					// +1 diff
					if ((kw.Value.OldId = LastRev(s)) != "")
					{
						log += "* [[Kép:Nuvola apps kblackbox.png|10px]] " + string.Format(ArticleLogLink, s) + " hozzáadva ("
							+ newArt.Quality.ToString().ToLower() + (newArt.Importance != null ? ", " + newArt.Importance.ToString().ToLower() : "") + ")\n";
						newArt.Date = DateTime.Today;
						diffs++;
						retArts.Add(kw.Key, newArt);
					}
				}
				else // Már meglévő szócikk
				{
					// Átmásoljuk a dátumot és az oldid-t az oldArts-ból, hogy ha nincs rá szükség, ne kelljen újra letölteni a lap laptörténetét
					newArt.OldId = oldArts[s].OldId;
					newArt.Date = oldArts[s].Date;

					// Megnézzük, hogy javult-e a besorolás
					if (newArt.Quality.Value > oldArts[s].Quality.Value)
					{
						if ((newArt.OldId = LastRev(s)) != "")
						{
							log += "* [[Kép:Crystal Clear app clean.png|10px]] " + string.Format(ArticleLogLink, s) + " besorolása javult ("
							+ oldArts[s].Quality.ToString().ToLower() + " → " + newArt.Quality.ToString().ToLower() + ")\n";
							newArt.Date = DateTime.Today;
							newArt.OldId = LastRev(newArt.Name);
							retArts.Add(kw.Key, newArt);
						}
						else
						{
							log += "* [[Kép:Nuvola apps error alt.svg|10px]] <span style=\"color:red;\">''" + string.Format(ArticleLogLink, s) + " eltávolítva.''</span>\n";
						}
						newArt.Date = DateTime.Today;
						diffs++;
					}
					// Megnézzük, hogy romlott-e a besorolás
					else if (newArt.Quality.Value < oldArts[s].Quality.Value)
					{
						if ((newArt.OldId = LastRev(s)) != "")
						{
							log += "* [[Kép:Crystal Clear app error.png|10px]] " + string.Format(ArticleLogLink, s) + " besorolása romlott ("
							+ oldArts[s].Quality.ToString().ToLower() + " → " + newArt.Quality.ToString().ToLower() + ")\n";
							newArt.Date = DateTime.Today;
							newArt.OldId = LastRev(newArt.Name);
							retArts.Add(kw.Key, newArt);
						}
						else
						{
							log += "* [[Kép:Nuvola apps error alt.svg|10px]] <span style=\"color:red;\">''" + string.Format(ArticleLogLink, s) + " eltávolítva.''</span>\n";
						}
						newArt.Date = DateTime.Today;
						diffs++;
					}
					else // ha nincs változás, módosítás nélkül visszarakjuk a listába
					{
						retArts.Add(kw.Key, newArt);
					}
				}
			}

			// Megnézzük mit töröltek még (aminél a vitalapról lett eltávolítva az értékelősablon)
			foreach (string s in oldArts.Keys)
			{
				if (!newArts.ContainsKey(s))
				{
					log += "* [[Kép:Nuvola apps error alt.svg|10px]] <span style=\"color:red;\">''" + string.Format(ArticleLogLink, s) + " eltávolítva.''</span>\n";
				}
			}

			if (diffs == 0) log += ":''Az előző futtatás óta nem történt változás.''\n";

			newArts = retArts;

			return log;
		}

		static string PrintStat(int[,] counter, string key)
		{
			string ret = "{{Cikkértékelés/Statisztika\n";
			if (key != "") ret += " | téma = " + key;
			for (int i = 0; i < QualityCategories.Count; i++)
			{
				for (int j = 0; j < ImportanceCategories.Count; j++)
				{
					ret += " | " + i.ToString() + "_" + j.ToString() + " = " + counter[i, j].ToString() + "\n";
				}
			}
			ret += "}}<noinclude>[[" + string.Format(QualityCat, key) + "|*Statisztika]]</noinclude>";
			return ret;
		}
		static string PrintQualityList(Dictionary<string, Article> arts, string key)
		{
			int curpage = 1;
			string log = "";
			List<string> pageTexts = new List<string>();
			List<int> pagesOnTexts = new List<int>();
			string pageText = "";
			int onPage = 0;

			var list = new List<Article>(arts.Values);
			list.Sort();

			foreach (Article article in list)
			{
				pageText += "{{Cikkértékelés/Értékelés | szócikk = " + article.Name +
													 " | oldid = " + article.OldId +
													 " | dátum = " + article.Date.ToString("yyyy-MM-dd") +
													 " | besorolás = " + article.Quality.Value +
													 " | fontosság = " + (article.Importance == null ? "0" : article.Importance.Value.ToString()) + "}}\n";
				onPage++;
				if (onPage == 80)
				{
					pageTexts.Add(pageText);
					pagesOnTexts.Add(onPage);
					curpage++;
					pageText = "";
					onPage = 0;
				}
			}
			if (pageText != "")
			{
				pageTexts.Add(pageText);
				pagesOnTexts.Add(onPage);
			}
			for (int i = 0; i < pageTexts.Count; i++)
			{
				string header = "{{Cikkértékelés/Táblázatfejléc}}\n";



				if (pageTexts.Count > 1)
				{
					Console.WriteLine("{0}. oldal elmentése ...", i + 1);
					string navi = "{{Cikkértékelés/Navigátorfejléc | téma = " + key + " | oldal = " + (i + 1).ToString() +
							  " | összesen = " + pageTexts.Count.ToString() + "}}\n\n";
					string footer = "{{Cikkértékelés/Lábléc}}\n\n";
					Page curPage = new Page(huwiki, string.Format(WPAssessmentCore + "/{1}", key, i + 1));
					curPage.Save(navi + header + pageTexts[i] + footer + navi, "Bot: táblázat frissítése", false);
					log += string.Format("* [[Wikipédia:Cikkértékelési műhely/{0} szócikkek/{1}|{1}. oldal]] ({2} szócikk)\n",
								   key, i + 1, pagesOnTexts[i]);
				}
				else
				{
					string navi = "{{Sablon:Cikkértékelés/Fejléc|téma=" + key + "}}\n\n";
					string footer = "{{Cikkértékelés/Lábléc | összesen = " + arts.Count.ToString() + "}}\n\n";

					Page curPage = new Page(huwiki, string.Format(WPAssessmentCore, key));
					curPage.Save(navi + header + pageTexts[i] + footer + navi, "Bot: táblázat frissítése", false);
					log += string.Format("* [[Wikipédia:Cikkértékelési műhely/{0} szócikkek/{1}|{1}. oldal]] ({2} szócikk)\n",
								   key, i + 1, pagesOnTexts[i]);
				}
			}
			return log;
		}
	}
}
