// ReSharper disable InconsistentNaming
namespace Reaparr.PublicAPI.Contracts;

public enum TorznabCategoryId
{
    // Reserved
    Reserved = 0,

    // Console
    Console = 1000,
    Console_NDS = 1010,
    Console_PSP = 1020,
    Console_Wii = 1030,
    Console_XBox = 1040,
    Console_XBox360 = 1050,
    Console_Wiiware = 1060,
    Console_XBox360_DLC = 1070,

    // Movies
    Movies = 2000,
    Movies_Foreign = 2010,
    Movies_Other = 2020,
    Movies_SD = 2030,
    Movies_HD = 2040,
    Movies_UHD = 2045,
    Movies_BluRay = 2050,
    Movies_3D = 2060,
    Movies_WEBDL = 2070,

    // Audio
    Audio = 3000,
    Audio_MP3 = 3010,
    Audio_Video = 3020,
    Audio_Audiobook = 3030,
    Audio_Lossless = 3040,

    // PC
    PC = 4000,
    PC_0day = 4010,
    PC_ISO = 4020,
    PC_Mac = 4030,
    PC_Mobile_Other = 4040,
    PC_Games = 4050,
    PC_Mobile_iOS = 4060,
    PC_Mobile_Android = 4070,

    // TV
    TV = 5000,
    TV_Foreign = 5020,
    TV_SD = 5030,
    TV_HD = 5040,
    TV_UHD = 5045,
    TV_Other = 5050,
    TV_Sport = 5060,
    TV_Anime = 5070,
    TV_Documentary = 5080,

    // XXX
    XXX = 6000,
    XXX_DVD = 6010,
    XXX_WMV = 6020,
    XXX_XviD = 6030,
    XXX_x264 = 6040,

    // Other
    Other = 7000,
    Other_Misc = 7010,
    Other_EBook = 7020,
    Other_Comics = 7030,

    // 100000+ reserved for Custom (site-specific, intentionally not enum-defined)
}
