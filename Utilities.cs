using System.Security.Cryptography;
using System.Text;

public class Utilities
{
    public static byte[] StringToByteArray(string hex)
    {
        return Enumerable
            .Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    public static string ConvertDeviceIdToHexString(string deviceId)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(deviceId);
        return ConvertByteArrayToHexString(bytes);
    }

    private static readonly char[] hexChars = "0123456789ABCDEF".ToCharArray();

    public static string ConvertByteArrayToHexString(byte[] byteArray)
    {

        char[] hexCharsArray = new char[byteArray.Length * 2];
        for (int i = 0; i < byteArray.Length; i++)
        {
            byte b = byteArray[i];
            int j = i * 2;
            hexCharsArray[j] = hexChars[(b & 0xFF) >> 4];
            hexCharsArray[j + 1] = hexChars[b & 0x0F];
        }
        return new string(hexCharsArray);
    }

    public static string GenerateAPIHash(string deviceId, long currentTimeMS)
    {
        long currentTimeMSMod = currentTimeMS * (long)1151;

        string currentTimeMSModString = currentTimeMSMod.ToString();

        // Bytes from hash
        string hashBytes =
            "6335623233363937313165623664343133363237663963623737646365643262353934666633323231346366313637373932656361343765";
        string l =
            "65393130393261643565613265303330633230316365396163343337336636623536356137383432";
        byte[] bytes = Encoding.UTF8.GetBytes(currentTimeMSModString);

        string bytesHex = ConvertByteArrayToHexString(bytes);

        string deviceIdHexString = ConvertDeviceIdToHexString(deviceId);

        string concat = hashBytes + l + bytesHex + hashBytes + deviceIdHexString;

        byte[] bytesToHash = StringToByteArray(concat);

        byte[] hash = SHA1.HashData(bytesToHash);

        return ConvertByteArrayToHexString(hash);
    }

    public static string GenerateRandomDeviceId()
    {
        // Generate random hex of 16 bytes
        byte[] randomBytes = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return ConvertByteArrayToHexString(randomBytes);
    }

    public static string GenerateRandomUUID()
    {
        Guid uuid = Guid.NewGuid();
        return uuid.ToString();
    }
}
