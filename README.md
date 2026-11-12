# Aurora

Aurora is a Windows desktop AI companion application built with C# and WPF.

## Documentation

The full project documentation is in **[docs/README.md](docs/README.md)**.

It covers:

- Project overview and companion architecture
- AI providers and model configuration
- Voice and image generation
- Weather, web search, and Spotify
- SmartThings, Home Assistant, Hubitat, and Alexa
- Local Windows tools and scoped file access
- Memory and settings
- Setup and development instructions
- Known limitations and future work
- Project structure

## Security and recovery

Aurora includes profile recovery and protected recovery-code storage:

- Profile recovery-code generation and protected verifier storage
- Recovery-code verification using PBKDF2-SHA256 with 600,000 iterations
- Encrypted recovery snapshots using AES-256-GCM
- Detached RSA-SHA256 signature verification
- Constant-time verification for sensitive recovery and pairing values

Recovery codes are not stored in plaintext by Aurora. Users are responsible for keeping their generated recovery code in a secure offline location.

## Windows ↔ Android connector

Aurora also contains the foundation for a secure Windows ↔ Android connector:

- Signed device-pairing requests using ECDSA P-256
- ECDH-based session-key derivation
- AES-256-GCM encrypted connector envelopes
- Sequence and replay protection
- Trusted-device storage and revocation
- Local UDP discovery metadata
- Capability and device fingerprint exchange
- A documented shared protocol for future Android implementation

Discovery is treated as untrusted metadata; a device must be authenticated and trusted before protected communication is established.

## Project

- **Platform:** Windows
- **UI:** WPF
- **Language:** C#
- **Target:** .NET 10
- **Repository:** `Sktler/Aurora-os`

For the complete guide, see **[docs/README.md](docs/README.md)**.
