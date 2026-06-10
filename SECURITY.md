# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 0.1.x   | Yes       |

## Reporting a vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report vulnerabilities privately through either channel:

- [GitHub Security Advisories](https://github.com/adamy/BotWire/security/advisories/new) (preferred)
- The Object IT contact form: <https://www.objectit.co.nz/contact> — mention "security" so we can prioritise it

Include:
- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof-of-concept
- Affected versions
- Any suggested fix (optional)

We aim to acknowledge reports within **3 business days** and complete an initial assessment within **14 days**. Fix timelines depend on severity; we will keep you informed of progress.

## How fixes are delivered

Security fixes are released as patch versions of the affected NuGet packages, accompanied by a GitHub Security Advisory describing the issue and affected version range.

## Disclosure policy

We follow coordinated disclosure. Please allow us reasonable time to patch before public disclosure. We will credit reporters in the release notes unless anonymity is requested.

## Scope

The following are in scope:

- `BotWire.Core`, `BotWire.AspNetCore`, `BotWire.Channels.Email` NuGet packages
- The embedded chat widget (`npm/botwire-js`)
- Denial-of-service vulnerabilities in BotWire code — e.g. regular-expression DoS in the PII or prompt-injection guards, or rate-limiter bypass

The following are **out of scope**:

- Vulnerabilities in third-party dependencies (report these upstream; we track them via Dependabot)
- Issues affecting only sample code in `samples/` — unless the sample demonstrates an insecure usage pattern that the documentation recommends
- Social engineering, or volumetric denial-of-service attacks against GitHub or other services hosting this project

## AI-specific security considerations

BotWire forwards user messages to an LLM provider you configure. The library includes prompt-injection and PII guards, but these are best-effort defences. Deployers are responsible for:

- Choosing a trustworthy LLM provider and reviewing their security posture
- Ensuring their knowledge-base documents do not contain sensitive data that should not be sent to an LLM
- Complying with applicable data-protection obligations for customer messages

If you discover a bypass of the built-in prompt-injection or PII guards, please report it via the process above.
