#region Using statements
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using RightEdge.Common;
using RightEdge.Common.ChartObjects;
using RightEdge.Indicators;
using System.IO;
#endregion

// TODO.1: zaktualizowac notowania i liste akcji delisted (na podstawie katalogu "C:\\Trading Data\\Stocks\\NDExport\\US Equities Delisted"
// dodac dane do C:\\Trading Data\\Stocks\\US_Text\\Delisted Securities", a polaczone wyniki zapisac w "C:\\Trading Data\\US Equities Delisted"
// to powinno dac liste wycofanych do stycznia 2022

// TODO.2: mam juz liste aktualnych skladnikow S&P 500 w folderze "Watchlist" S&P500
// mam tez w folderze S&P500 All since 1996 te akcje ktore nadal sa notowane, ale kiedys (kiedykolwiek) byly w S&P 500
// ---------- tutaj moze byc tak ze aktualnie jest symbol (np. AAB, dla przykladu, i on kiedys byl w S&P, ale to bylo inne AAB, 
// a tymczasem AAB wrocilo do S&P500, ale to bylo inne AAB - inna firma) --- czy to ma znaczenie?? tylko jesli nigdy nie byly
// rownolegle notowane dwa AAB jednoczesnie
// teraz trzerba dodac wszystkie papiery (nadal notowane i juz nie notowane) ktore kiedys byly w sp500 do S&P500 All since 1996
// a wiec musze 1/ dodac wszystkie (jakie sie da) z nadal notowanych do S&P500 All since (ale nadal mam suvivorship bias)
// a także te ktore byly w s&p500 ale juz nie sa notowane (i dopiero znika suvivor ship bias

// TODO 3: dodac sprawdzanie symboli delisted (problem: w plikach sa duplikaty z roznymi datami delistingu 
// TODO 4: dodac sprzedawanie kiedy symbol jest w portfelu i okazuje sie ze opuszcza on S&P500 

//Historical Lists of S&P 500 components since 1996:
// https://github.com/fja05680/sp500

// kopiowanie juz wycofanych z listy tickerow ktore kiedykolwiek byly w sp500:
// w command line DOS:
//for /F "tokens=*" %A in (all_sp500_tickers_1996.txt) do copy "C:\Trading Data\Stocks\US_Text\Delisted Securities\"%A-*.csv "C:\Trading Data\sp500_delisted"

// a) sprawdzic brakujace w DELISTED? tickery - NP. ETFL
// b) dodac delisted wszystkie (czy wszystkie co byly w S&P500?)
// c) sprawdzic czy w symulacji mam wszystko to co mam w csv z komponentatmi historycznymi S&P500
// d) dodac logike w funkcji sprawdzajacej sklad indeksu, sprawdzanie dla nazwy z data (np. WRB-202010) - odcinanie daty, sprawdzanie dat poprzednich itp


// jesli chce odtworzy symulacje z ksiazki, to data start date = 29.03.1998, simulation end date 06.01.2015

// CAGR dla S%P 500 ----  http://www.moneychimp.com/features/market_cagr.htm

#region System class
public class MySystem : MySystemBase
{
	#region dict2
	Dictionary<DateTime, Dictionary<string, int>> dict2 = new Dictionary<DateTime, Dictionary<string, int>>(){
			/*	
			{new DateTime(2019,04,11),new Dictionary<string,int>{ 
				{"AMBRA",315},{"ENTER",112},{"INSTALKRK",250},{"MARVIPOL",667},
				{"OPONEO.PL",84},{"VOTUM",443},{"VISTAL",564},
				
				{"ATAL",95},{"CLNPHARMA",54},{"EKOEXPORT",229},
				{"PATENTUS",1091} ,{"SYNEKTIK",210},
				{"TORPOL",423},{"WORKSERV",1596}
			
			}},
			
			{new DateTime(2019,04,25),new Dictionary<string,int>{ 
				{"IMPEXMET",999},{"RADPOL",2030}			//,{"GPRE",810}	
			
			}},
			{new DateTime(2019,05,03),new Dictionary<string,int>{ 
				{"GPRE",810}	
			
			}},
			{new DateTime(2019,05,10),new Dictionary<string,int>{ 
				{"MOSTALWAR",485}	,{"AUTOPARTN",934}	
			}},

			{new DateTime(2016,11,17),new Dictionary<string,int>{{"egs",428}, {"mdg",13}, {"bri",126} }},
			{new DateTime(2017,01,26),new Dictionary<string,int>{{"agt",394},{"bml",2452},{"pzu",145} }}
			*/
		};
	#endregion
	

	//publis static double USE_MA100 = 1.0;
	public static double PORTFOLIO_RISK_RATIO;
	public static double MAX_GAP = 0.15;
	public static string INDEX_NAME = "$SPX"; //wig - bo swig80 ma większy drawdown, efekty niekoniecznie lepsze
	public static System.DayOfWeek simulationTradingDayOfWeek = System.DayOfWeek.Thursday;
	public static DateTime dataOstatniejSesji = DateTime.Now.AddDays(-1); 
	//public static double maxAccVal=0.0;
	//public static double sredniaSila = 0.0;
	//public static double minimalnaPlynnosc = 100000; // średnia za okres 60 dni
	public static bool printOutput = false; 
	public static string historicalSP500path = @"c:\Trading Data\S&P 500 Historical Components & Changes(10-18-2021).csv";
	public static SortedDictionary<DateTime, List<string>> dictSP500histComponents;
	//public static double currDrawdawn = 0.0;
	//public static DateTime doNotTradeBefore = DateTime.Parse("1900-01-30 00:00:00");
	//public static int ileMiesiecyCooldown = 3;

	public static int nweek = 0;
	public override void Startup()
	{			
		//PositionManager.CalculateMAEMFE = true;
		
		#region DayOfWeek
		switch ((int)(SystemData.SystemParameters["DayOfWeek"]))
		{
			case 1:
				simulationTradingDayOfWeek = System.DayOfWeek.Monday;
				break;
			case 2:
				simulationTradingDayOfWeek = System.DayOfWeek.Tuesday;
				break;
			case 3:
				simulationTradingDayOfWeek = System.DayOfWeek.Wednesday;
				break;
			case 4:
				simulationTradingDayOfWeek = System.DayOfWeek.Thursday;
				break;
			case 5:
				simulationTradingDayOfWeek = System.DayOfWeek.Friday;
				break;
			default:
				simulationTradingDayOfWeek = System.DayOfWeek.Thursday;
				break;
		}
		#endregion
		
		PORTFOLIO_RISK_RATIO = (((double)SystemData.SystemParameters["PortfolioRiskRatio"])/1000.0);
		// Perform initialization or set system wide options here
		//MIN_SIZE = (double)(SystemData.SystemParameters["MinSize"]);	
		//MAX_GAP = ((double)(SystemData.SystemParameters["MaxGap"]))/100.0;	
		
		DateTime dt = DateTime.Now.Date;
		// jeśli dziś czwartek, to przesuwam się na środę.
		
		while(dt.DayOfWeek != simulationTradingDayOfWeek) 
		{
			dt = dt.AddDays(-1);
		}
		dataOstatniejSesji = dt;
		if (printOutput) 
			Console.WriteLine("Data ost. sesji przed ost. trading day: "+dataOstatniejSesji);		
	}
	
	public override void Startup(SystemData data)
	{
		base.Startup(data);

        // zaladuj csv z historycznymi skladnikami S%P500
		using (StreamReader reader = new StreamReader(historicalSP500path))
		{

			dictSP500histComponents = new SortedDictionary<DateTime, List<string>>();
			while (!reader.EndOfStream)
			{
                string line = reader.ReadLine();
				string[] values = line.Split(',');
				DateTime myDate = DateTime.ParseExact(values[0], "yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
				List<string> lista = new List<string>(values);
				lista.RemoveAt(0);
				dictSP500histComponents[myDate] = lista;
			}
		}

		
		// tutaj pozostala moja wlasna logika (jesli cos trzeba jeszcze)
	}
	
	#region PoliczRanking()
	protected List<string> PoliczRanking()
	{
		Dictionary<Symbol, double> silySymboli = new Dictionary<Symbol, double>();

		// zapełniam ranking
//		sredniaSila = 0.0;
		int i=0;
		//foreach (string symbolName in SkladIndeksuNaDzis())
		foreach (MySymbolScript ss in this.SymbolScripts)
		{
			string symbolName= ss.Symbol.Name;
			if (!CzyWSkladzieIndeksu(symbolName))
			{
				continue;
			}
			/*
			try 
			{
			 	ss = this.SymbolScripts[symbolName];
			}
			catch (Exception e)
			{
				Console.WriteLine( e.Message );
			}
			if (ss == null )
			{
				if (printOutput) Console.WriteLine( symbolName +" nie mam tego symbolu");
				continue;
			}
			*/
			/*
			TODO - to jest niedobrze - powinno byc foreach 
				(mysymbolscript ss in sp500skladindeksu[okreslona data] -> tu trzeba po nazwie pobierac po kolei symbole z ysmulacji)
				w ten sposob mam gwarancje ze rozpatruje tylko symbole SP500 (max 500 w danym dniu
				to powinno tez mocno przyspieszyc symulacje.
			
			if (!CzyWSkladzieIndeksu(ss.Symbol.Name))
			{
				// interesuja nas tylko najsilniejsze spolki ze skladu indeksu S&P500, a nie wszystkie kiedykolwiek!!
				continue;
			}
			*/
			if (ss.Close == null || ss.Close.Count<1)
			{
				//if (printOutput) Console.WriteLine( symbolName +" nie ma danych");
				continue;
			}
			if (!double.IsNaN(ss.Close.Current))
			{
				if (!Double.IsNaN(ss.rank.Current) )//&& ss.rank.Current>0)
				{
					silySymboli[ss.Symbol] = ss.rank.Current;
//					sredniaSila += ss.rank.Current;
					i++;
				}
				//ss.OutputMessage( ss.SystemData.CurrentDate+": "+ ss.Symbol.Name+":  " + ss.rank.Current);
			}
		}
//		sredniaSila = sredniaSila / i;
		// sortuję po sile
		double procent = (double)SystemData.SystemParameters["Threshold Rank"];
		int n = (int) ( (procent/100.0) * silySymboli.Count); // czyli patrzymy na np. 20% najmocniejszych spółek ze wszystkich spółek, bez względu na kupowalność (ZA_ILE>2000zł)
		//n = Math.Max(20,n); // chcemy mieć zawsze min. 20 spółek. 
		
		////////////// UWAGA TO JEST TYCZASOWE TODO / ERROR!!! //////////////
		n = (int)SystemData.SystemParameters["Threshold Rank"]; // patrzymy na 50 najmocniejszych!
		//n=10; // patrzre na at dziesiec spolek
		//////////////////////////////////////////////////////////////////////////////////////////
		
		List<string> ls = new List<string>(n);
		if (printOutput) Console.WriteLine("== "+ n +" / "+silySymboli.Count +" == "+ procent +" [%] == ");//AVG: "+sredniaSila);
		i=0;
		
		foreach (var item in silySymboli.OrderByDescending(r => r.Value).Take(n))
		{
			ls.Add((string)item.Key);
			//Console.Write("{0}({1}), ",(string)item.Key, Math.Round((double)item.Value,1));
			//if (i%8==0)
			//	Console.WriteLine();
			//i++;
		}		
		

		if (printOutput) Console.WriteLine(".");
		if (printOutput) Console.WriteLine("================");
		
		return ls;
	}
	#endregion
	
	#region ZamykajPozycje()
	protected void ZamykajPozycje(List<string> sl)
	{
		foreach (Position p in this.PositionManager.GetOpenPositions())
		{
			// a) EXIT gdy spółka opuściła indeks albo wycofana (delisted)
			// b) EXIT gdy momentum rank jest poniżej TOP xx%
			// c) EXIT gdy jest poniżej SMA100
			// d) EXIT gdy był GAP>15%
			MySymbolScript ss = this.SymbolScripts[p.Symbol];

			string opisZamknieca = "";
			
						
			//MySymbolScript wig = this.SymbolScripts[INDEX_NAME];
			//IList<BarData> bars = ss.Bars.Items;
			
			if (ss.Close.Current < ss.lowestClose.Current) { opisZamknieca += " < lowestClose! "; }
			
//			if ((int)(SystemData.SystemParameters["UseMA100"])>0 &&
//				ss.Close.Current < ss.MA100.Current)		{ opisZamknieca += " Close < MA100";	}
			
			if ((int)(SystemData.SystemParameters["UseMaxGap"])>0 &&
				ss.gap.Current >= MAX_GAP)				{ opisZamknieca += " Luka > 15%";					}
			
			// na jakim teraz miejscu w rankingu jest aktualny symbol?
			if ( (int)(SystemData.SystemParameters["ZamykajSlabe"]) == 1 && !sl.Contains(ss.Symbol.Name)) //sl.IndexOf(ss.Symbol.Name)+1) < (int)(sl.Count/2)  )//!sl.Contains(ss.Symbol.Name) )		
			{ opisZamknieca += " poza listą najmocniejszych"; }
			
			if (!CzyWSkladzieIndeksu(ss.Symbol.Name)) { opisZamknieca += " Nie w Indeksie! "; }

			// sprawdzamy czy dzisiejszy close byl ponizej wczorajszego lowest low z 50 sesji
			if ((int)(SystemData.SystemParameters["UseLL"])>0 &&
				ss.Close.Current < ss.lowestClose.LookBack(1) ) { opisZamknieca += " < lowestClose! "; }
				

			//else if (ss.rank.Current<0.3)
			//{opisZamknieca = "siła mniejsza od 0.3"; }
			         //ss.OtherSymbols[INDEX_NAME].Close.Current< ss.OtherSymbols[INDEX_NAME].MA200.Current &&  
			
			if (opisZamknieca.Length>0)
			{
				p.CloseAtMarket(opisZamknieca);
				if (printOutput) System.Console.WriteLine("CLOSE "+p.Symbol.Name+ " "+opisZamknieca);
			}
		}	
	}
	#endregion ZamykajPozycje()
	
	#region RebalansOtwartych()
	protected void RebalansOtwartych()
	{
		MySymbolScript ss;
		long targetSize = 0;

		foreach (Position p in this.PositionManager.GetOpenPositions())
		{
			ss = this.SymbolScripts[p.Symbol];
			targetSize = GetSize(ss);
			long diff = targetSize - p.CurrentSize;
			if (targetSize>0 && (p.CurrentSize-targetSize)>0)//*ss.Close.Current>1000.0 /*&& Math.Abs( (p.CurrentSize-targetSize)/targetSize  ) > 0.05*/) // jesli roznica jest >0.05 balansuj
			{
				if (diff>0 && (diff/p.CurrentSize>0.05) ) // dokupić, o ile zmiana pozycji >10%
				{
				// NIE DOKUPUJĘ
					this.PositionManager.AddToPosition(p.ID, targetSize - p.CurrentSize, OrderType.MarketOnOpen,double.NaN, "rebalans +"+(targetSize - p.CurrentSize));
				}
				else if (diff<0 && (diff/p.CurrentSize>0.05) ) // sprzedać trochę, o ile zmiana pozycji >10%
				{
					long cursiz = p.CurrentSize;
					this.PositionManager.RemoveFromPosition(p.ID, p.CurrentSize-targetSize,OrderType.MarketOnOpen,double.NaN,"rebalans -"+(cursiz-targetSize)+", cursize"+cursiz+", targetsize ="+targetSize);
				}
			}
		}
	}
	#endregion 
	
	#region DrukujPortfel()
	protected void DrukujPortfel()
	{
		Console.WriteLine("Pozycji w portfelu: "+ this.PositionManager.GetOpenPositions().Count);
		Console.WriteLine("------------");
		
		SortedDictionary<double, Symbol> sd = new SortedDictionary<double, Symbol>();
		foreach(Position p in this.PositionManager.GetOpenPositions())
		{
			MySymbolScript ss = this.SymbolScripts[p.Symbol];
			if (!sd.ContainsValue(p.Symbol))
				sd.Add(ss.rank.Current, p.Symbol);
		}
		foreach (KeyValuePair<double, Symbol> kvp in sd.Reverse())
		{
			Console.Write(kvp.Value.Name+", ");		
		}
		Console.WriteLine(".");
		Console.WriteLine("------------");
	}
	#endregion
	
	#region GetSize()
	public long GetSize(MySymbolScript ss)
	{
		//long size1 = (long)((this.SystemData.AccountValue*PORTFOLIO_RISK_RATIO) / ss.atr.Current);
		
		// traktuje "PortfolioRiskRatio" jako "ile spolek miec w portfelu?
		double kwota = this.SystemData.AccountValue;
		double ile_spolek = (double)SystemData.SystemParameters["PortfolioRiskRatio"];
		long size2 = (long) ((kwota/ile_spolek)/ss.Close.Current);
		//long size2 = (long)( ss.smaVol.Current*((double)(SystemData.SystemParameters["ProcentPlynnosci"])/100.0));
		double cash = (this.SystemData.AccountValue- this.SystemData.CurrentEquity);
		if (size2*ss.Close.Current > cash)
		{
			size2 = (long)(cash/ss.Close.Current); //kupujemy mniejsza pozycje - za ta gotowke co zostala.
		}
		
		//return Math.Min(size1,size2); 
		return size2;
	}
	#endregion

	#region SkladIndeksuNaDzis()
	protected List<string> SkladIndeksuNaDzis()
	{
		// jesli walor na dzien dzisiejszy (CurrentDate) nie jest obecnie w S&P 500, to go nie kupuje
		List<string> listaSymboli;
		if (dictSP500histComponents.ContainsKey(this.SystemData.CurrentDate))
		{
			listaSymboli = dictSP500histComponents[this.SystemData.CurrentDate];
		}
		else // ta data nie pokrywa sie z data publikacji zmian w skladzie indeksu, trzeba poszukac najblizszej
		{
			// szukamy najblizszej wczesnijeszej daty
			var keys = new List<DateTime>(dictSP500histComponents.Keys);
			var index = keys.BinarySearch(this.SystemData.CurrentDate);

			if (~index - 1 < 0) throw Exception("obecna data " + this.SystemData.CurrentDate + " jest starsza niz historia indeksu!");

			//Console.WriteLine("najblizej jest " + keys[~index - 1]);
			// najblizsza (ostatnia, starsza) data zmiany skladu indeksu to
			DateTime lastIndexChangeDate = keys[~index - 1];
			//ostatni znany sklad ineksu to
			listaSymboli = dictSP500histComponents[lastIndexChangeDate];
		}
		return listaSymboli;
	}
	#endregion
	
	#region CzyWSkladzieIndeksu()
	protected bool CzyWSkladzieIndeksu(string symbolName)
	{
		if (SkladIndeksuNaDzis().Contains(symbolName))
		{ 
			// znaleziono, symbol jest na aktualnej liscie komponentow indeksu SP500
			return true;
		}
		else
		{
			//sprawdzamy jeszcze zdelistowane czyli np YNR-200010
			if (symbolName.Contains('-'))
			{
				string [] a = symbolName.Split('-');
				symbolName = a[0];
				
				if (a[1].Length<2) return false; // przpyadki typu "AFS-A", gdzie oczywiscie -A to nie jest data!
				DateTime d = DateTime.ParseExact(a[1], "yyyyMM",System.Globalization.CultureInfo.InvariantCulture);
				if (d.Year < this.SystemData.CurrentDate.Year ||
					(d.Year == this.SystemData.CurrentDate.Year && d.Month == this.SystemData.CurrentDate.Month)
					) 
					return false; // z punktu widzenia symulacji ten symbol juz byl wycofany
				//if (d.Year > this.SystemData.CurrentDate.Year) return false; // rok wycofania pozniejszy od obecnego roku - mozna rozpatrzyc
				// ale musimy tez uzyc daty!!, bo moze byc ten sam symbol w roznych okresach notowany, wiec jego data z nazwy to gorne ograniczenie
				// trzeba porownac date z nazwy z data aktualna i jesli jest aktualna ponizej to ten symbol rozpatrujemy!!
				return CzyWSkladzieIndeksu(symbolName); // pytam samego siebie, ale dla samej nazwy ticker'a po odcieciu - np. YNR
			}
			// tego symbolu nie ma na liscie S&P500 (sprzedac albo nie kupowac)!
			return false;
		}

	}
	#endregion
	
	
	public override void NewBar()
	{
		base.NewBar();
		
		
		//if (printOutput) System.Console.WriteLine("data: "+this.SystemData.CurrentDate);
			
		/*
		maxAccVal = Math.Max(this.SystemData.AccountValue, maxAccVal);
		currDrawdawn =(maxAccVal-this.SystemData.AccountValue)/maxAccVal;
		if (currDrawdawn> 0.15)
		{
			
			this.PositionManager.CloseAllPositions();
			currDrawdawn = 0.0; //reset drawdown and hope that system won't come back to buying...
			maxAccVal = 0.0;
			doNotTradeBefore = this.SystemData.CurrentDate.AddMonths(ileMiesiecyCooldown);
			System.Console.WriteLine("## DRAWDOWN >0.15, następne kupowanie od: " +doNotTradeBefore);
			return;
		}
		*/
		
		// TODO: zapisywać wszystki transakcje w excelu albo w pliku tekstowym
		// potem ten program ma sobie załadować te transakcje do "dictionary"
		// a potem wykonać je zgodnie z datami wykonania, 
		// w ten sposób moja symulacja będzie dokładnie wiedziała co kupiłem co sprzedałem i pokaże faktyczny zysk		
		
		#region inicjalizacja portfela
		if ( ((int)(SystemData.SystemParameters["ZacznijOdDzisiaj"])) == 1) 
		{
			// tu wrzucamy transakcje stanowiące portfel początkowy dla daty currentbar'a
			Dictionary<string,long> dict = new Dictionary<string,long>();
			foreach (DateTime d in dict2.Keys)
			{
				if (SystemData.CurrentDate.Date.CompareTo(d) == 0 )
				{
					Dictionary<string,int> dict3= dict2[d];
					foreach (string ticker in dict3.Keys)
					{
						dict.Add(ticker,dict3[ticker]);
					}
				}
			}				

			foreach(KeyValuePair<string,long> kvp in dict)
			{
				//tutaj otwieram pozycje
				RightEdge.Common.PositionSettings ps = new PositionSettings();
				ps.Symbol = this.SymbolScripts[kvp.Key].Symbol;
				ps.PositionType = PositionType.Long;
				ps.OrderType = OrderType.MarketOnOpen;
				ps.Size = kvp.Value;
				
				Position p = this.PositionManager.OpenPosition(ps);
				System.Console.WriteLine("++"+ps.Symbol.Name+ " "+ps.Size+ " Error: -"+p.Error+"-"); 
			}
				
			//System.Console.WriteLine(
			//	"Data: "+SystemData.CurrentDate+
			//	"      "+dataOstatniejSesji+
			//	"      "+(SystemData.CurrentDate.Date.CompareTo(dataOstatniejSesji)<0)
			//	);
			
			// Less than zero 	This instance is earlier than value.
			//System.Console.WriteLine(SystemData.CurrentDate.Date.CompareTo(DateTime.Now.Date.AddDays(-1)) !=0);
			
			if (SystemData.CurrentDate.Date.CompareTo(dataOstatniejSesji)<0 
				//&&
				// reunion - chcę handlować 02.maja (czwartek, jest sesja), 
			    // gdy 01. maja nie było sesji
				//SystemData.CurrentDate.Date.CompareTo(DateTime.Now.Date.AddDays(-1)) !=0
				
				) //data ostatniej sesji + 1 dzień
			{	
				//System.Console.WriteLine(" nie kupuj " +SystemData.CurrentDate.Date); 
				return;
				// nie pozwalam systemowi kupić nic innego niż w ten dzień faktycznie kupiłem
			}

			
		}
		#endregion
		
		if ( SystemData.CurrentDate.DayOfWeek != simulationTradingDayOfWeek 
			//&&
			// reunion - chcę handlować 02.maja (czwartek, jest sesja), 
			// gdy 01. maja nie było sesji
			
			//SystemData.CurrentDate.Date.CompareTo(DateTime.Now.Date.AddDays(-1)) !=0
			)
		{
			
			//System.Console.WriteLine("nie handluję, bo "+SystemData.CurrentDate.DayOfWeek+"  to nie jest "+simulationTradingDayOfWeek);
			return; 
		}

		//System.Console.WriteLine("Data pierwszej automatycznej transakcji systemu: "+SystemData.CurrentDate.Date);

		nweek++;
		/*
		if ((nweek%4)==0) // co czwarty tydzien sprawdzamy
		{
			return;
		}
		*/
		List<string> sl = PoliczRanking(); // lista najmocniejszych z S&P500
		// 2. sprawdzam czy zamknąć jakieś pozycje
		
		//System.Console.WriteLine("srednia sila: "+sredniaSila.ToString());
		
		this.ZamykajPozycje(sl);
		
		// tutaj już mamy pozamykane pozycje, jeśli trzeba było wyjść
		// a może trzeba tylko dokonać rebalansowania?
		// 3. rebalans otwartych pozycji
		if ((nweek%2)==0 && (int)(SystemData.SystemParameters["Rebalansuj"]) > 0)
			this.RebalansOtwartych();

		// 4. otwieranie nowych pozycji za dostępną gotówkę...
		MySymbolScript idx = this.SymbolScripts[INDEX_NAME];
		
		if( idx.Close.Current < idx.MA200.Current ) // ... ale tylko jeśli indeks (np. S&P500 jest > MA200)
		{
			if (printOutput) System.Console.WriteLine("index pod MA200 - BRAK NOWYCH ZAKUPÓW");
			return; // nie dokonujemy żadnych nowych zakupów
		}
		
		/*
		if (doNotTradeBefore.CompareTo(this.SystemData.CurrentDate)>0)
		{
			System.Console.WriteLine("nie handluję do" + doNotTradeBefore+" a jest: " +this.SystemData.CurrentDate+" - BRAK NOWYCH ZAKUPÓW");
			//this.RebalansOtwartych();
			return; // nie dokonujemy żadnych nowych zakupów
		}
		*/
		
//		if (sredniaSila <  ((double)(SystemData.SystemParameters["sredniaSilaMin"]))/100.0) // najlepiej działa 60 (0.6)
//		{
//			if (printOutput) System.Console.WriteLine("siła rynku poniżej wymaganej - BRAK NOWYCH ZAKUPÓW");
//			return;
//		}
		
		
		double dowydania = this.SystemData.CurrentCapital;
		//System.Console.WriteLine("Na początek do wydania: "+dowydania);

		foreach (string symbolName in sl)
		{				
			MySymbolScript ss =  this.SymbolScripts[symbolName];

			if (ss.Symbol.Name == INDEX_NAME ) // tych nie kupuję, ale muszę mieć w zbiorze dla sprawdzania warunków zakupu
				continue;

//			if ((int)(SystemData.SystemParameters["UseLL"])>0 &&
//				ss.Close.Current < ss.lowestClose.LookBack(1) ) 
//			{ 
//				//Console.WriteLine(symbolName+" - < lowestClose!");
//				continue;
//			}

			if (this.PositionManager.GetOpenPositions(ss.Symbol).Count > 0 ) // otwieramy tylko jak jeszcze nie mamy
				continue;
			
			if ( !CzyWSkladzieIndeksu(symbolName))
			{
				//Console.WriteLine(symbolName+" - tego symbolu nie ma na liscie S&P500 , nie mozemy go kupowac!");
				continue;
			}


//			if (
//				(int)(SystemData.SystemParameters["UseMA100"])>0 &&
//				ss.Close.Current < ss.MA100.Current)  // unikam kupowania pod średnią ze 100 sesji
//				continue;
			if ((int)(SystemData.SystemParameters["UseMaxGap"])>0 &&
				ss.gap.Current > MAX_GAP)  // unikam kupowania po niedawnej dużej luce
				continue;

 
			RightEdge.Common.PositionSettings ps = new PositionSettings();
			ps.Symbol = ss.Symbol;
			ps.PositionType = PositionType.Long;
			ps.OrderType = OrderType.Market;
			ps.StopLossType = TargetPriceType.RelativeRatio;

			ps.Size = GetSize(ss); 
			
			if (true)//ps.Size*ss.Close.Current>MIN_SIZE ) // kupuję tylko te spółki za >2000zł
			{
				if (printOutput) System.Console.WriteLine("BUY {0} {1} za ~{2}$",ps.Size, ps.Symbol.Name, ps.Size*ss.Close.Current);
				if (printOutput) System.Console.WriteLine(" zostało jeszcze do wydania: "+dowydania);				
				if (ps.Size*ss.High.Current < dowydania)
				{
					Position p = this.PositionManager.OpenPosition(ps);
					dowydania = dowydania - (ps.Size*ss.High.Current);
					//if (p.Error != "")
					//	System.Console.WriteLine("**"+ps.Symbol.Name+ " "+ps.Size+ " Error: -"+p.Error+"-"); 
				}
			}
		}
	}

    private Exception Exception(string v)
    {
        throw new NotImplementedException();
    }
}
#endregion

#region MySymbolScript class
public class MySymbolScript : MySymbolScriptBase
{
	public UserSeries rank;
	public UserSeries gap;
	public Lowest lowestClose;
	//public SMA lowestLowSMA;
	public SMA MA200;
	//public SMA MA100;

	public AverageTrueRange atr; 
	//SystemParameters param;
	
	UserSeries lnClose;
	
	#region Funkcje statystyczne
	public static double [] x ;
	
	public static double GetRSquared(double [] array1, double [] array2)
	{
	double R = 0;
	
	try
	{
		// sum(xy)
		double sumXY = 0;
		for (int c = 0; c <= array1.Length - 1; c++)
		{
			sumXY = sumXY + array1[c] * array2[c];
		}
	
		// sum(x)
		double sumX = 0;
		for (int c = 0; c <= array1.Length - 1; c++)
		{
			sumX = sumX + array1[c];
		}
	
		// sum(y)
		double sumY = 0;                
		for (int c = 0; c <= array2.Length - 1; c++)
		{
			sumY = sumY + array2[c];
		}
		
		// sum(x^2)
		double sumXX = 0;
		for (int c = 0; c <= array1.Length - 1; c++)
		{
			sumXX = sumXX + array1[c] * array1[c];
		}                
		
		// sum(y^2)
		double sumYY = 0;
		for (int c = 0; c <= array2.Length - 1; c++)
		{
			sumYY = sumYY + array2[c] * array2[c];
		}
	
		// n
		int n = array1.Length;
	
		R = (n * sumXY - sumX * sumY) / (Math.Pow((n * sumXX - Math.Pow(sumX, 2)), 0.5) * Math.Pow((n * sumYY - Math.Pow(sumY, 2)), 0.5));
	}
	catch (Exception ex)
	{
		throw (ex);
	}
	
	return R * R;
	}
	
	public static double GetSlope(double[] xArray, double[] yArray)
	{
		if (xArray == null)
			throw new ArgumentNullException("xArray");
		if (yArray == null)
			throw new ArgumentNullException("yArray");
		if (xArray.Length != yArray.Length)
			throw new ArgumentException("Array Length Mismatch");
		if (xArray.Length < 2)
			throw new ArgumentException("Arrays too short.");
	
		double n = xArray.Length;
		double sumxy = 0, sumx = 0, sumy = 0, sumx2 = 0;
		for (int i = 0; i < xArray.Length; i++)
		{
			sumxy += xArray[i] * yArray[i];
			sumx += xArray[i];
			sumy += yArray[i];
			sumx2 += xArray[i] * xArray[i];
		}
		return ((sumxy - sumx * sumy / n) / (sumx2 - sumx * sumx / n));
	}
	#endregion
	
	#region Startup()
	public override void Startup()
	{
//		param = SystemData.SystemParameters;
		
		gap = new UserSeries();
		gap.ChartSettings.ChartPaneName ="Max Gap over 60 sessions";
		gap.ChartSettings.ShowInChart= true;
		gap.ChartSettings.DisplayName = "Max Gap over 60 sessions";
		gap.ChartSettings.Color = Color.Red;

		if (this.Symbol.Name == MySystem.INDEX_NAME )
		{
			MA200 = new SMA(200, Close);
			MA200.ChartSettings.Color = Color.Blue;
			//MA200.ChartSettings.ShowInChart = false;
		}

//		MA100 = new SMA(100,Close);
//		MA100.ChartSettings.Color = Color.Red;

		lowestClose = new Lowest(50,Low);
		//lowestLowSMA = new SMA(10,lowestClose);
		
		atr= new AverageTrueRange(20);
		atr.ChartSettings.ShowInChart =false;
		
		lnClose = new UserSeries();
		lnClose.ChartSettings.ShowInChart = false;
		
		rank = new UserSeries();
		rank.ChartSettings.ChartPaneName ="Rank";
		rank.ChartSettings.ShowInChart= true;
		rank.ChartSettings.DisplayName = "Rank";
		rank.ChartSettings.Color = Color.Violet;
		
		// zainicjalizuj x
		
		List<double> number = new List<double>();
		int period = (int)(SystemData.SystemParameters["DniDoSily"]);
		for (int i=0; i<period; i++ )
		{
			number.Add(i);
		}
		
		x = number.ToArray(); 
		/*
		{0,1,2,3,4,5,6,7,8,9,10,
								11,12,13,14,15,16,17,18,19,20,
								21,22,23,24,25,26,27,28,29,30,
								31,32,33,34,35,36,37,38,39,40,
								41,42,43,44,45,46,47,48,49,50,
								51,52,53,54,55,56,57,58,59,60,
								61,62,63,64,65,66,67,68,69,70,
								71,72,73,74,75,76,77,78,79,80,
								81,82,83,84,85,86,87,88,89
		};
		*/
		
	}
	#endregion
	
	public override void NewBar()
	{
		int period = (int)(SystemData.SystemParameters["DniDoSily"]);
		if (Bars.Count<(int)(SystemData.SystemParameters["DniDoSily"])
//			|| double.IsNaN(MA200.Current) 
//			|| double.IsNaN(MA100.Current) 
//			|| double.IsNaN(smaVol.Current) 
			
			)
			return;
		
		// 1. skonstruować custom indicator który daje wartość 1 gdy spółka jest w WIG i daje 0 gdy nie jest
		// narazie nic
		
		// 2. policzyć największą lukę jaką spółka zrobiła w przeciągu ostatnich 90 dni
		

		if ((int)(SystemData.SystemParameters["UseMaxGap"])>0 )
		{
			double max60 = Math.Abs(Open.LookBack(0)-Close.LookBack(1))/ Close.LookBack(1);
			double current_gap = 0.0;
			for (int i = 0; i < 60; i++)
			{
				current_gap = Math.Abs(Open.LookBack(i)-Close.LookBack(i+1))/ Close.LookBack(i+1) ; 
				if  ( current_gap > max60) 
					max60 = current_gap;
			}
			gap.Current = max60;
		}
		
		// obliczamy rzeczy do rankingu
		lnClose.Current = Math.Log(Close.Current);
		
		double [] y = new double [period];
		for (int i = 0; i < period; i++)
		{
			y[i] = this.lnClose.LookBack(period-1-i);
		}
		double annualized_slope = Math.Pow(Math.Exp(GetSlope(x,y)),250)-1;
		rank.Current = annualized_slope * GetRSquared(x,y);
	
	}

}
#endregion