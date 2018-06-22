self.webprop['TriggitFeedGenerator']={     
    'TriggitFeedGenerator.exe.config':
	{
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=TriggitFeedGenerator;",  
        "OdysseyCommerceDB"					:"server=brodbsql;database=Odyssey_Commerce;User ID=prodadmin;Password=!prod@dmin;Application Name=TriggitFeedGenerator;",  
		"ProductsDB"						:"server=brodbsql;database=ProductsDB;User ID=prodadmin;Password=!prod@dmin;Application Name=TriggitFeedGenerator;", 
		"AllowIncrementalRuns"				:"true",
		"AllowItemErrorsInFiles"			:"false",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
		"MaximumThreadsToUse"				:"3",
		"LimitTo100Products"				:"false",
		"BaseUrl"							:"https://www.chapters.indigo.ca",
		"ImgBaseUrl"						:"https://dynamic.indigoimages.ca",
		"GzipFiles"							:"true",
		"FtpHost"							:"sdax.indigo.ca",
		"FtpDropFolderPath"					:"/",
		"FtpUserName"						:"sftp_Triggit",
		"FtpUserPassword"					:"sNeebR2q",
		"OutputFolderPath"					:"G:\Bronte\MarketingFeeds\TriggitFeedGenerator\outputs",
		"SkipHasImageCheck"					:"false",
		"TestIncrementalRunFromDate"		:"", 
		"OutputFileMoveMethod"				:"2",
		"TargetFolderPath"					:"\\\prdftpsdax01\sftp_Triggit",
		"InputFolderPath"					:"G:\support\\autobat\TriggitFeedGenerator\input-files", 
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"triggitfeedgenerator@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Triggit Feed Generator Notification Email",  
    }, 
}

self.webprop['GoogleCategoryImporter']={     
    'GoogleCategoryImporter.exe.config':
	{
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=GoogleCategoryImporter;",
		"InputFileFolderPath"				:"G:\support\\autobat\GoogleCategoryImporter\input-files",
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"googlecategoryimporter@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Google Category Importer Notification Email",  
    }, 
}

self.webprop['IndigoFeedSystemDataProcessor']={     
    'IndigoFeedSystemDataProcessor.exe.config':
	{
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=IndigoFeedSystemDataProcessor;", 
		"EktronDB"							:"Data Source=PRDBROWSESQL;Initial Catalog=cms400_v85sp2;Trusted_Connection=Yes;Application Name=IndigoFeedSystemDataProcessor;Connection Timeout=90;enlist=false;",
		"RecosDB"							:"data source=PRDBROWSESQL;initial catalog=RecosDB;Integrated Security=SSPI;Application Name=IndigoFeedSystemDataProcessor;Connection Timeout=90;enlist=false;",  		
		"EndecaUrl"							:"10.3.2.158",
		"EndecaPort"						:"15000",
		
		"NetNamedPipeBinding_ICatalogueServiceContract"             :"net.pipe://csi.indigo.ca/CatalogueService.svc",
		"NetNamedPipeBinding_ICustomerListServiceContract"          :"net.pipe://csi.indigo.ca/CustomerListService.svc",
		"NetNamedPipeBinding_IMerchAdminServiceContract"            :"net.pipe://csi.indigo.ca/MerchAdminService.svc",
		"NetNamedPipeBinding_IMerchandisingServiceContract"         :"net.pipe://csi.indigo.ca/MerchandisingService.svc",
		"NetNamedPipeBinding_IOrderServiceContract"                 :"net.pipe://csi.indigo.ca/OrderService.svc",
		"NetNamedPipeBinding_IProfileServiceContract"               :"net.pipe://csi.indigo.ca/ProfileService.svc",
		"NetNamedPipeBinding_IReferenceDataServiceContract"         :"net.pipe://csi.indigo.ca/ReferenceDataService.svc",
		"NetNamedPipeBinding_ISocialServiceContract"                :"net.pipe://csi.indigo.ca/SocialService.svc",
		"NetNamedPipeBinding_IStoreServiceContract"                 :"net.pipe://csi.indigo.ca/StoreService.svc",
		"NetNamedPipeBinding_IAdminServiceContract"                 :"net.pipe://csi.indigo.ca/AdminService.svc",
		"NetNamedPipeBinding_IEcommerceServiceContract"             :"net.pipe://csi.indigo.ca/EcommerceService.svc",

		"netTcpBinding_ICatalogueServiceContract"             :"net.tcp://csi.indigo.ca:12345/CatalogueService.svc",
		"netTcpBinding_ICustomerListServiceContract"          :"net.tcp://csi.indigo.ca:12345/CustomerListService.svc",
		"netTcpBinding_IMerchAdminServiceContract"            :"net.tcp://csi.indigo.ca:12345/MerchAdminService.svc",
		"netTcpBinding_IMerchandisingServiceContract"         :"net.tcp://csi.indigo.ca:12345/MerchandisingService.svc",
		"netTcpBinding_IOrderServiceContract"                 :"net.tcp://csi.indigo.ca:12345/OrderService.svc",
		"netTcpBinding_IProfileServiceContract"               :"net.tcp://csi.indigo.ca:12345/ProfileService.svc",
		"netTcpBinding_IReferenceDataServiceContract"         :"net.tcp://csi.indigo.ca:12345/ReferenceDataService.svc",
		"netTcpBinding_ISocialServiceContract"                :"net.tcp://csi.indigo.ca:12345/SocialService.svc",
		"netTcpBinding_IStoreServiceContract"                 :"net.tcp://csi.indigo.ca:12345/StoreService.svc",
		"netTcpBinding_IAdminServiceContract"                 :"net.tcp://csi.indigo.ca:12345/AdminService.svc",
		"netTcpBinding_IEcommerceServiceContract"             :"net.tcp://csi.indigo.ca:12345/EcommerceService.svc",
    }, 
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"IndigoFeedSystemDataProcessor@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Indigo Feed Management Daily Data Processor Notification Email",  
    }, 
}

self.webprop['GooglePlaFeedGenerator']={     
    'GooglePlaFeedGenerator.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=GooglePLAGenerator;",  
        "OdysseyCommerceDB"					:"server=brodbsql;database=Odyssey_Commerce;User ID=prodadmin;Password=!prod@dmin;Application Name=GooglePLAGenerator;",  
		"ProductsDB"						:"server=brodbsql;database=ProductsDB;User ID=prodadmin;Password=!prod@dmin;Application Name=GooglePLAGenerator;", 
		
		"RunMode"							:"2",
		"MaximumThreadsToUse"				:"3",
		"LimitTo100Products"				:"false",
		"GzipFiles"							:"true",
		"FtpHost"							:"sdax.indigo.ca",
		"FtpDropFolderPath"					:"",
		"FtpUserName"						:"sftp_GoogleCS",
		"FtpUserPassword"					:"REPLACEME",
		"OutputFolderPath"					:"G:\Bronte\MarketingFeeds\GooglePlaFeedGenerator\outputs",
		"AncillaryOutputFolderPath"			:"G:\Bronte\MarketingFeeds\GooglePlaFeedGenerator\\ancillary",
		"InputFolderPath"					:"G:\support\\autobat\GooglePlaFeedGenerator\input-files",
		"SkipHasImageCheck"					:"false",
		"OutputFileMoveMethod"				:"2",
		"TargetFolderPath"					:"\\\prdftpsdax01\sftp_GoogleCS\PLA",
		"TargetFolderPathSecondary"			:"\\\prdftpsdax01\sftp_YahooPLA",
		"FtpHostSecondary"					:"sdax.indigo.ca",
		"FtpDropFolderPathSecondary"		:"",
		"FtpUserNameSecondary"				:"sftp_YahooPLA",
		"FtpUserPasswordSecondary"			:"aHgv5ceM",		
		"AllowItemErrorsInFiles"			:"true",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"PlaFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Pla Feed Generator Notification Email",  
    }, 
}

self.webprop['IndigoToGoogleTaxonomyMapping']={     
    'IndigoToGoogleTaxonomyMapping.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=IndigoToGoogleTaxonomyMapping;",  
		
		"InputFileFolderPath"					:"G:\support\\autobat\IndigoToGoogleTaxonomyMapping\input-files",
	},
	
	'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"IndigoToGoogleTaxonomyMapper@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Indigo to Google Taxonomy Mapper Notification Email",  
    }, 
}

self.webprop['NewGoogleCategoriesForIndigoCategoriesImporter']={     
    'NewGoogleCategoriesForIndigoCategoriesImporter.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=NewGoogleCategoriesForIndigoCategoriesImporter;",  
		"InputFileFolderPath"				:"G:\Bronte\MarketingFeeds\NewGoogleCategoriesForIndigoCategoriesImporter\input-files",
	}, 
	'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"NewGoogleCategoriesForIndigoCategoriesImporter@indigo.ca",  
        "SMTPSubject"						:"[PROD] - Google Taxonomy Mapping Importer Notification Email",  
    },
}

self.webprop['CjFeedGenerator']={     
    'CjFeedGenerator.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;",  
        "OdysseyCommerceDB"					:"server=brodbsql;database=Odyssey_Commerce;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;",  
		"ProductsDB"						:"server=brodbsql;database=ProductsDB;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;", 
		
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
		"TestIncrementalRunFromDate"		:"",
		"InputFolderPath"					:"G:\support\\autobat\CjFeedGenerator\input-files",
		"AncillaryOutputFolderPath"			:"G:\Bronte\MarketingFeeds\CjFeedGenerator\\ancillary",
		"GzipFiles"							:"false",
		"OutputFolderPath"					:"G:\Bronte\MarketingFeeds\CjFeedGenerator\outputs",
		"OutputFileMoveMethod"				:"1",
		"FtpHost"							:"ftp://datatransfer.cj.com",
		"FtpDropFolderPath"					:"/",
		"FtpUserName"						:"1666237", 
		"FtpUserPassword"					:"MTwvwRkX",
		"TargetFolderPath"					:"\\\prdftpsdax01\sftp_CJ",
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"CjFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[PROD] - CjFeedGenerator Notification Email",  
    }, 
}

self.webprop['BVRatingImporter']={     
    'BVRatingImporter.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=BVRatingImporter;",  
		"FtpHost"							:"ftp://ftp.bazaarvoice.com",
		"NoFtpDownload"						:"false",
		"DownloadFolderPath"				:"G:\Bronte\BVRatingImporter\working",
	},
	
	'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"BVRatingImporter@indigo.ca",  
        "SMTPSubject"						:"[PROD] - BVRatingImporter Email",  
    }, 
}

self.webprop['RRFeedGenerator']={     
    'RRFeedGenerator.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;",  
        "OdysseyCommerceDB"					:"server=brodbsql;database=Odyssey_Commerce;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;",  
		"ProductsDB"						:"server=brodbsql;database=ProductsDB;User ID=prodadmin;Password=!prod@dmin;Application Name=CjFeedGenerator;", 
		
		"LimitTo100Products"				:"false",
		"MaximumThreadsToUse"				:"3",
		"SkipHasImageCheck"					:"false",
		"OutputFileMoveMethod"				:"1",
		"FtpHost"							:"ftp://ftp.richrelevance.com",
		"FtpDropFolderPath"					:"/",
		"FtpUserName"						:"indigo",
		"FtpUserPassword"					:"jH^.8Pf2eK",
		"ZipFiles"							:"true",
		"InputFolderPath"					:"G:\support\\autobat\RRFeedGenerator\input-files",
		"OutputFolderPath"					:"G:\support\\autobat\RRFeedGenerator\outputs",
		"WorkingDirectory"					:"G:\support\\autobat\RRFeedGenerator\working",
		"ProductFilesPath"					:"G:\support\\autobat\RRFeedGenerator\working\productfiles",
		"AttributeFilesPath"				:"G:\support\\autobat\RRFeedGenerator\working\\Attributefiles",
		"ProductCategoryFilesPath"			:"G:\support\\autobat\RRFeedGenerator\working\productcategoryfiles",
		"AllowIncrementalRuns"				:"false", 
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp", 
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"RRFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[PROD] - RRFeedGenerator Email",  
    }, 
}

self.webprop['DynamicCampaignsFeedGenerator']={     
    'DynamicCampaignsFeedGenerator.exe.config':
	{		
		"BatchDB"							:"server=brodbsql;database=BatchDB;User ID=prodadmin;Password=!prod@dmin;Application Name=DynamicCampaignsFeedGenerator;",  
        "OdysseyCommerceDB"					:"server=brodbsql;database=Odyssey_Commerce;User ID=prodadmin;Password=!prod@dmin;Application Name=DynamicCampaignsFeedGenerator;",  
		"ProductsDB"						:"server=brodbsql;database=ProductsDB;User ID=prodadmin;Password=!prod@dmin;Application Name=DynamicCampaignsFeedGenerator;", 
		
		"LimitTo100Products"				:"false",
		"MaximumThreadsToUse"				:"3",
		"GzipFiles"							:"true",
		"InputFolderPath"					:"G:\support\\Autobat\\DynamicCampaignsFeedGenerator\input-files",
		"OutputFolderPath"					:"G:\Bronte\MarketingFeeds\DynamicCampaignsFeedGenerator\outputs",
		"OutputFileMoveMethod"				:"1",
		"FtpHost"							:"filehub.marinsoftware.com",
		"FtpDropFolderPath"					:"",
		"FtpUserName"						:"indigo_dc",
		"FtpUserPassword"					:"bfIN94pG",
		"SkipHasImageCheck"					:"false",
		"AllowItemErrorsInFiles"			:"true",
		"AllowDeletedCategories"			:"true",
		"AllowRuleOptimizations"			:"true",
		"AllowRuleEntryRemovals"			:"true",
		"AllowIEnumerableRuleEvaluations"	:"true",
		"BaseUrl"							:"https://www.chapters.indigo.ca",
	},

		
    'Log4net.config':
	{
		"SMTPHost"							:"mx1.indigo.corp",  
		"SMTPTo"							:"FeedGenerationTeam@indigo.ca",  
        "SMTPFrom"							:"DynamicCampaignsFeedGenerator@indigo.ca",  
        "SMTPSubject"						:"[PROD] - DynamicCampaignsFeedGenerator Email",  
    }, 
}