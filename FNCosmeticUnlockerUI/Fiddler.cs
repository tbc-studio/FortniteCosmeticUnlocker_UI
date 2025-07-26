using Fiddler;

namespace FNCosmeticUnlockerUI;

internal class Fiddler
{
    public static bool Setup()
    {
        return CertMaker.createRootCert() && CertMaker.trustRootCert();
    }
}
