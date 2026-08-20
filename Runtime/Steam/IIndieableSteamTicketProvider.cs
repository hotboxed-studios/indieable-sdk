using System;

namespace IndieableSdk.Steam
{
    // Implement this interface in the host game using Steamworks.NET or another
    // Steam integration. The Indieable package intentionally has no Steamworks
    // dependency and never embeds a publisher key.
    public interface IIndieableSteamTicketProvider
    {
        void GetTicketForWebApi(
            string identity,
            Action<string> onTicketHex,
            Action<string> onError);
    }
}
