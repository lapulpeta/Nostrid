internal static class TestUtils
{

	public static string GetRandomNostrId()
	{
		byte[] bytes = new byte[32];
		Random.Shared.NextBytes(bytes);
		return Convert.ToHexString(bytes).ToLower();
	}

}