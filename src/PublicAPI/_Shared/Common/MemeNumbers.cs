namespace Reaparr.PublicAPI;

public static class MemeNumberGenerator
{
    private static readonly int[] _memeNumbers =
    [
        7, // lucky number
        13, // bad luck
        21, // blackjack or meme age
        32, // windows nostalgia
        42, // the answer to life
        69, // nice
        100, // keep it 100
        101, // intro course
        111, // binary aesthetic
        222, // angel number
        333, // repeating pattern
        404, // not found
        420, // blaze it
        451, // Fahrenheit reference
        500, // internal error
        666, // the beast
        777, // jackpot
        8008, // boobs (classic)
        80085, // enhanced boobs
        9001, // it’s over 9000!
        10000, // round number satisfaction
        12345, // easy password
        20000, // arbitrary large meme
        6969, // double nice
        1337, // elite hacker
        1984, // Orwell reference
        2007, // peak internet era
        3000, // I love you 3000
        8080, // port humor
        123456789, // muscle memory typing
        69420, // ultimate meme combo
        314, // pi-ish
        2718, // e-ish
        5150, // insane
        2020, // cursed year
        2077, // cyberpunk prophecy
    ];

    public static int GetMemeNumber(string stableId)
    {
        var hash = uint.Parse(
            stableId.AsSpan("reaparr-".Length, 8),
            System.Globalization.NumberStyles.HexNumber,
            provider: null
        );
        return _memeNumbers[(int)(hash % (uint)_memeNumbers.Length)];
    }
}
