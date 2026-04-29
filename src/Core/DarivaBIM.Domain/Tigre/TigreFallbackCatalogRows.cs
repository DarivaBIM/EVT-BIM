using System.Collections.Generic;

namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// Embedded fallback catalog used when the JSON file cannot be loaded.
    /// Kept in Domain because the data is part of the business knowledge;
    /// only the disk loader lives in the Persistence layer.
    /// </summary>
    public static class TigreFallbackCatalogRows
    {
        public static IEnumerable<TigreRawCatalogRow> All()
        {
            return new List<TigreRawCatalogRow>
            {
                new TigreRawCatalogRow { Description = "Série Reforçada", DiameterMm = 40,  Code = 11054323 },
                new TigreRawCatalogRow { Description = "Série Reforçada", DiameterMm = 50,  Code = 11054420 },
                new TigreRawCatalogRow { Description = "Série Reforçada", DiameterMm = 75,  Code = 11054528 },
                new TigreRawCatalogRow { Description = "Série Reforçada", DiameterMm = 100, Code = 11055010 },
                new TigreRawCatalogRow { Description = "Série Reforçada", DiameterMm = 150, Code = 11051600 },

                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 40,  Code = 11111700 },
                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 50,  Code = 11030602 },
                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 75,  Code = 11030904 },
                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 100, Code = 11031030 },
                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 150, Code = 11031501 },
                new TigreRawCatalogRow { Description = "Série Normal", DiameterMm = 200, Code = 11032036 },

                new TigreRawCatalogRow { Description = "Redux Laranja", DiameterMm = 40,  Code = 100002786 },
                new TigreRawCatalogRow { Description = "Redux Laranja", DiameterMm = 50,  Code = 100002787 },
                new TigreRawCatalogRow { Description = "Redux Laranja", DiameterMm = 75,  Code = 100002788 },
                new TigreRawCatalogRow { Description = "Redux Laranja", DiameterMm = 100, Code = 100002789 },
                new TigreRawCatalogRow { Description = "Redux Laranja", DiameterMm = 150, Code = 100002790 },

                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 20,  Code = 10120209 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 25,  Code = 10120250 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 32,  Code = 10120322 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 40,  Code = 10120403 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 50,  Code = 10120500 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 60,  Code = 10120608 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 75,  Code = 10120756 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 85,  Code = 10120853 },
                new TigreRawCatalogRow { Description = "Soldável Marrom", DiameterMm = 110, Code = 10121035 },

                new TigreRawCatalogRow { Description = "ClicPEX Monocamada", DiameterMm = 16, Code = 300000774 },
                new TigreRawCatalogRow { Description = "ClicPEX Monocamada", DiameterMm = 20, Code = 300000775 },
                new TigreRawCatalogRow { Description = "ClicPEX Monocamada", DiameterMm = 25, Code = 300000776 },
                new TigreRawCatalogRow { Description = "ClicPEX Monocamada", DiameterMm = 32, Code = 300000777 },

                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 15,  Code = 17000152 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 22,  Code = 17000225 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 28,  Code = 17000284 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 35,  Code = 17001086 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 42,  Code = 17001108 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 54,  Code = 17001132 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 73,  Code = 17001515 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 89,  Code = 17001531 },
                new TigreRawCatalogRow { Description = "Aquatherm", DiameterMm = 114, Code = 17001558 },

                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 25, Code = 17020056 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 32, Code = 17020080 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 40, Code = 17020110 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 50, Code = 17020153 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 60, Code = 17020188 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 75, Code = 17020226 },
                new TigreRawCatalogRow { Description = "CPVC TIGREFire", DiameterMm = 85, Code = 17020250 },

                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 32,  Code = 17010565 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 40,  Code = 17010581 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 50,  Code = 17010603 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 63,  Code = 17020620 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 75,  Code = 17010646 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 90,  Code = 17010670 },
                new TigreRawCatalogRow { Description = "PPR PN12", DiameterMm = 110, Code = 17010689 },

                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 20,  Code = 17010026 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 25,  Code = 17010042 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 32,  Code = 17010069 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 40,  Code = 17010085 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 50,  Code = 17010107 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 63,  Code = 17010123 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 75,  Code = 17010140 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 90,  Code = 17010174 },
                new TigreRawCatalogRow { Description = "PPR PN20", DiameterMm = 110, Code = 17010182 },

                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 20, Code = 17010328 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 25, Code = 17010344 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 32, Code = 17010360 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 40, Code = 17010387 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 50, Code = 17010409 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 63, Code = 17010425 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 75, Code = 17010441 },
                new TigreRawCatalogRow { Description = "PPR PN25", DiameterMm = 90, Code = 17010476 },
            };
        }
    }
}
