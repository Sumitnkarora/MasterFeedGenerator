self.webprop['TriggitFeedGenerator']={     
    'TriggitFeedGenerator.exe.config':
	{
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
        "OdysseyCommerceDB"					:"Data Source=165sq-9120;Initial Catalog=Odyssey_Commerce;Integrated Security=SSPI;Connection Timeout=90;",  
		"ProductsDB"						:"Data Source=165sq-9120;Initial Catalog=ProductsDB;Integrated Security=SSPI;Connection Timeout=90;", 
		"AllowIncrementalRuns"				:"true",
		"AllowItemErrorsInFiles"			:"false",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
		"MaximumThreadsToUse"				:"3",
		"LimitTo100Products"				:"false",
		"BaseUrl"							:"https://qa.indigo.ca",
		"ImgBaseUrl"						:"https://dynamic.qa.indigoimages.ca",
		"GzipFiles"							:"true",
		"FtpHost"							:"QAFTPSERVER",
		"FtpDropFolderPath"					:"QAFTPDROPFOLDERPATH",
		"FtpUserName"						:"QAFTPUSERNAME",
		"FtpUserPassword"					:"QAFTPPASSWORD",
		"OutputFolderPath"					:"S:\MarketingFeeds\TriggitFeedGenerator\outputs",
		"SkipHasImageCheck"					:"true",
		"TestIncrementalRunFromDate"		:"", 
		"OutputFileMoveMethod"				:"0",
		"TargetFolderPath"					:"S:\MarketingFeeds\MovedFiles\TriggitFeedGenerator",
		"InputFolderPath"					:"S:\MarketingFeeds\TriggitFeedGenerator\input-files", 
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"IT-ServiceEmailTest@indigo.ca",  
        "SMTPFrom"							:"triggitfeedgenerator@indigo.ca",  
        "SMTPSubject"						:"[QA] - Triggit Feed Generator Notification Email",  
    }, 
}

self.webprop['GoogleCategoryImporter']={     
    'GoogleCategoryImporter.exe.config':
	{
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;", 
		"InputFileFolderPath"				:"S:\MarketingFeeds\GoogleCategoryImporter\input-files",
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"googlecategoryimporter@indigo.ca",  
        "SMTPSubject"						:"[QA] - Google Category Importer Notification Email",  
    }, 
}

self.webprop['IndigoFeedSystemDataProcessor']={     
    'IndigoFeedSystemDataProcessor.exe.config':
	{
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;enlist=false;",  
		"EktronDB"							:"Data Source=QABROWSESQL;Initial Catalog=cms400_v85sp2;Integrated Security=SSPI;Application Name=IndigoFeedSystemDataProcessor;Connection Timeout=90;enlist=false;", 
		"RecosDB"							:"data source=QABROWSESQL;initial catalog=RecosDB;Integrated Security=SSPI;Application Name=IndigoFeedSystemDataProcessor;Connection Timeout=90;enlist=false;",
		"EndecaUrl"							:"172.17.8.83",
		"EndecaPort"						:"15000",
				
		"NetNamedPipeBinding_ICatalogueServiceContract"       :"net.pipe://csi.qa.indigo.ca/CatalogueService.svc",
		"NetNamedPipeBinding_ICustomerListServiceContract"    :"net.pipe://csi.qa.indigo.ca/CustomerListService.svc",
		"NetNamedPipeBinding_IMerchAdminServiceContract"      :"net.pipe://csi.qa.indigo.ca/MerchAdminService.svc",
		"NetNamedPipeBinding_IMerchandisingServiceContract"   :"net.pipe://csi.qa.indigo.ca/MerchandisingService.svc",
		"NetNamedPipeBinding_IOrderServiceContract"           :"net.pipe://csi.qa.indigo.ca/OrderService.svc",
		"NetNamedPipeBinding_IProfileServiceContract"         :"net.pipe://csi.qa.indigo.ca/ProfileService.svc",
		"NetNamedPipeBinding_IReferenceDataServiceContract"   :"net.pipe://csi.qa.indigo.ca/ReferenceDataService.svc",
		"NetNamedPipeBinding_ISocialServiceContract"          :"net.pipe://csi.qa.indigo.ca/SocialService.svc",
		"NetNamedPipeBinding_IStoreServiceContract"           :"net.pipe://csi.qa.indigo.ca/StoreService.svc",
		"NetNamedPipeBinding_IAdminServiceContract"           :"net.pipe://csi.qa.indigo.ca/AdminService.svc",
		"NetNamedPipeBinding_IEcommerceServiceContract"       :"net.pipe://csi.qa.indigo.ca/EcommerceService.svc",

		"netTcpBinding_ICatalogueServiceContract"             :"net.tcp://csi.qa.indigo.ca:12345/CatalogueService.svc",
		"netTcpBinding_ICustomerListServiceContract"          :"net.tcp://csi.qa.indigo.ca:12345/CustomerListService.svc",
		"netTcpBinding_IMerchAdminServiceContract"            :"net.tcp://csi.qa.indigo.ca:12345/MerchAdminService.svc",
		"netTcpBinding_IMerchandisingServiceContract"         :"net.tcp://csi.qa.indigo.ca:12345/MerchandisingService.svc",
		"netTcpBinding_IOrderServiceContract"                 :"net.tcp://csi.qa.indigo.ca:12345/OrderService.svc",
		"netTcpBinding_IProfileServiceContract"               :"net.tcp://csi.qa.indigo.ca:12345/ProfileService.svc",
		"netTcpBinding_IReferenceDataServiceContract"         :"net.tcp://csi.qa.indigo.ca:12345/ReferenceDataService.svc",
		"netTcpBinding_ISocialServiceContract"                :"net.tcp://csi.qa.indigo.ca:12345/SocialService.svc",
		"netTcpBinding_IStoreServiceContract"                 :"net.tcp://csi.qa.indigo.ca:12345/StoreService.svc",
		"netTcpBinding_IAdminServiceContract"                 :"net.tcp://csi.qa.indigo.ca:12345/AdminService.svc",
		"netTcpBinding_IEcommerceServiceContract"             :"net.tcp://csi.qa.indigo.ca:12345/EcommerceService.svc",
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"IndigoFeedSystemDataProcessor@indigo.ca",  
        "SMTPSubject"						:"[QA] - Indigo Feed Management Daily Data Processor Notification Email",  
    }, 
}

self.webprop['GooglePlaFeedGenerator']={     
    'GooglePlaFeedGenerator.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
        "OdysseyCommerceDB"					:"Data Source=165sq-9120;Initial Catalog=Odyssey_Commerce;Integrated Security=SSPI;Connection Timeout=90;",  
		"ProductsDB"						:"Data Source=165sq-9120;Initial Catalog=ProductsDB;Integrated Security=SSPI;Connection Timeout=90;", 
		
		"RunMode"							:"1",
		"MaximumThreadsToUse"				:"3",
		"LimitTo100Products"				:"true",
		"GzipFiles"							:"false",
		"FtpHost"							:"QAFTPSERVER",
		"FtpDropFolderPath"					:"QAFTPDROPFOLDERPATH",
		"FtpUserName"						:"QAFTPUSERNAME",
		"FtpUserPassword"					:"QAFTPPASSWORD",
		"OutputFolderPath"					:"S:\MarketingFeeds\GooglePlaFeedGenerator\outputs",
		"AncillaryOutputFolderPath"			:"S:\MarketingFeeds\GooglePlaFeedGenerator\ancillary",
		"SkipHasImageCheck"					:"true",
		"OutputFileMoveMethod"				:"0",
		"TargetFolderPath"					:"S:\MarketingFeeds\MovedFiles\GooglePlaFeedGenerator",
		"TargetFolderPathSecondary"			:"S:\MarketingFeeds\MovedFiles\YahooPlaFeedGenerator",
		"FtpHostSecondary"					:"QAFTPSERVER2",
		"FtpDropFolderPathSecondary"		:"QAFTPDROPFOLDERPATH2",
		"FtpUserNameSecondary"				:"QAFTPUSERNAME2",
		"FtpUserPasswordSecondary"			:"QAFTPPASSWORD2",
		"InputFolderPath"					:"S:\MarketingFeeds\GooglePlaFeedGenerator\input-files", 
		"AllowItemErrorsInFiles"			:"true",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"GooglePlaFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[QA] - Google Pla Feed Generator Notification Email",  
    }, 
}

self.webprop['IndigoToGoogleTaxonomyMapping']={     
    'IndigoToGoogleTaxonomyMapping.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  

		"InputFileFolderPath"				:"S:\MarketingFeeds\IndigoToGoogleTaxonomyMapping\input-files",
	},
}

self.webprop['NewGoogleCategoriesForIndigoCategoriesImporter']={     
    'NewGoogleCategoriesForIndigoCategoriesImporter.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
		"InputFileFolderPath"				:"S:\MarketingFeeds\NewGoogleCategoriesForIndigoCategoriesImporter\input-files",
	}, 
	'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"jyim@indigo.ca",  
        "SMTPFrom"							:"NewGoogleCategoriesForIndigoCategoriesImporter@indigo.ca",  
        "SMTPSubject"						:"[QA] - Google Taxonomy Mapping Importer Notification Email",  
    },
}

self.webprop['CjFeedGenerator']={     
    'CjFeedGenerator.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
        "OdysseyCommerceDB"					:"Data Source=165sq-9120;Initial Catalog=Odyssey_Commerce;Integrated Security=SSPI;Connection Timeout=90;",  
		"ProductsDB"						:"Data Source=165sq-9120;Initial Catalog=ProductsDB;Integrated Security=SSPI;Connection Timeout=90;", 
		
		"FeedId"							:"3",
		"AllowIncrementalRuns"				:"true",
		"AllowItemErrorsInFiles"			:"false",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
		"MaximumThreadsToUse"				:"4",
		"LimitTo100Products"				:"false",
		"SkipHasImageCheck"					:"true",
		"AllowItemErrorsInFiles"			:"false",
		"TestIncrementalRunFromDate"		:"2012-01-01",
		"InputFolderPath"					:"S:\MarketingFeeds\CjFeedGenerator\input-files",
		"AncillaryOutputFolderPath"			:"S:\MarketingFeeds\CjFeedGenerator\\ancillary",
		"GzipFiles"							:"false",
		"OutputFolderPath"					:"S:\MarketingFeeds\CjFeedGenerator\outputs",
		"OutputFileMoveMethod"				:"0",
		"FtpHost"							:"QAFTPSERVER",
		"FtpDropFolderPath"					:"QAFTPFOLDER",
		"FtpUserName"						:"QAFTPUSERNAME", 
		"FtpUserPassword"					:"QAFTPUSERPASSWORD",
		"TargetFolderPath"					:"S:\MarketingFeeds\MovedFiles\CjFeedGenerator",
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"CjFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[QA] - CjFeedGenerator Notification Email",  
    }, 
}

self.webprop['BVRatingImporter']={     
    'BVRatingImporter.exe.config':
	{
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;", 
		"FtpHost"							:"ftp://ftp-stg.bazaarvoice.com",
		"NoFtpDownload"						:"false",
		"DownloadFolderPath"				:"S:\MarketingFeeds\BVRatingImporter\working",
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"BVRatingImporter@indigo.ca",  
        "SMTPSubject"						:"[QA] - BVRatingImporter Email",  
    }, 
}

self.webprop['RRFeedGenerator']={     
    'RRFeedGenerator.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
        "OdysseyCommerceDB"					:"Data Source=165sq-9120;Initial Catalog=Odyssey_Commerce;Integrated Security=SSPI;Connection Timeout=90;",  
		"ProductsDB"						:"Data Source=165sq-9120;Initial Catalog=ProductsDB;Integrated Security=SSPI;Connection Timeout=90;", 
		
		"LimitTo100Products"				:"true",
		"MaximumThreadsToUse"				:"3",
		"SkipHasImageCheck"					:"true",
		"OutputFileMoveMethod"				:"0",
		"FtpHost"							:['skip'],
		"FtpDropFolderPath"					:['skip'],
		"FtpUserName"						:['skip'],
		"FtpUserPassword"					:['skip'],
		"ZipFiles"							:"false",
		"InputFolderPath"					:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\\input-files",
		"OutputFolderPath"					:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\\outputs",
		"WorkingDirectory"					:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\working",
		"ProductFilesPath"					:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\working\\productfiles",
		"AttributeFilesPath"				:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\working\\attributefiles",
		"ProductCategoryFilesPath"			:"\\\\165SQ-9120\s$\MarketingFeeds\RRFeedGenerator\working\\productcategoryfiles",
		"AllowIncrementalRuns"				:"false", 
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"RRFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[QA] - RRFeedGenerator Email",  
    }, 
}

self.webprop['DynamicCampaignsFeedGenerator']={     
    'DynamicCampaignsFeedGenerator.exe.config':
	{		
		"BatchDB"							:"Data Source=165sq-9120;Initial Catalog=BatchDB;Integrated Security=SSPI;Connection Timeout=90;",  
        "OdysseyCommerceDB"					:"Data Source=165sq-9120;Initial Catalog=Odyssey_Commerce;Integrated Security=SSPI;Connection Timeout=90;",  
		"ProductsDB"						:"Data Source=165sq-9120;Initial Catalog=ProductsDB;Integrated Security=SSPI;Connection Timeout=90;", 
		
		"LimitTo100Products"				:"true",
		"MaximumThreadsToUse"				:"3",
		"GzipFiles"							:"false",
		"InputFolderPath"					:"\\\\165SQ-9120\s$\MarketingFeeds\DynamicCampaignsFeedGenerator\input-files",
		"OutputFolderPath"					:"\\\\165SQ-9120\s$\MarketingFeeds\DynamicCampaignsFeedGenerator\Output",
		"OutputFileMoveMethod"				:"0",
		"FtpHost"							:['skip'],
		"FtpDropFolderPath"					:['skip'],
		"FtpUserName"						:['skip'],
		"FtpUserPassword"					:['skip'],
		"SkipHasImageCheck"					:"true",
		"AllowItemErrorsInFiles"			:"true",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
		"BaseUrl"							:"https://qa.indigo.ca",
	},

		
    'Log4net.config':
	{
			"SMTPHost"						:"mx1.indigo.corp",  
		"SMTPTo"							:"cyilmaz@indigo.ca,jyim@indigo.ca",  
        "SMTPFrom"							:"DynamicCampaignsFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[QA] - DynamicCampaignsFeedGenerator Email",  
    }, 
}
