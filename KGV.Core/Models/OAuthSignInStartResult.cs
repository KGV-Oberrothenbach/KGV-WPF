using System;

namespace KGV.Core.Models
{
    public sealed record OAuthSignInStartResult(Uri AuthUri, string PkceVerifier);
}
