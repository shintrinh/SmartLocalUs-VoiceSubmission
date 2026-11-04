# AI Voice Receptionist — call-only submission repo

> **Pre-existing work disclosure (read first):** the SMS part of this product existed before the hackathon window and is **not in this repo**. SMS code and its git history live in the original product repo, [shintrinh/SmartLocalUs](https://github.com/shintrinh/SmartLocalUs), as a separate pre-existing service. This repo contains only the voice call work built 26 October – 4 November 2026 for the Meta Global AI Developer Hackathon (Startup track, Build for Your World).

## What was built in the window (this repo)

A voice agent for US local businesses (HVAC example) that answers a missed call, books a job in 5 stages, saves a lead, and hands off to a human when asked:

1. **Voice webhooks** (`src/AirwayHVAC.SmsAi.Api/Functions/InboundVoiceFunction.cs`): `POST /api/webhooks/twilio/voice/inbound`, `/gather`, `/status`.
2. **Stage machine** (`ConversationFlowEngine`): Issue → Address → Urgency → Appointment → Confirm → Completed. Completed is deterministically gated on all 4 fields (Issue, Address, Urgency, PreferredTime).
3. **AI reply layer** (`IAiResponder`, `ConversationOrchestrator`): AI reply replaces the static stage reply when available; static replies alone can also complete a call, so flow completion alone is never presented as proof of an AI tier.
4. **Tool calling**: exactly one tool, `book_appointment` (required: day, service; optional: caller_name, time), exactly one tool round. Any other function name returns `{"status":"unknown_tool"}` and cannot loop, book, or corrupt a lead.
5. **Human handoff**: handoff keywords (representative / human / agent / operator / person) short-circuit before the orchestrator and return TwiML `<Dial>` to the configured owner number. A real ringing, bridged Dial call was proven 02/11/2026 via a verified US number answered through a VoIP app: after "representative", the AI said "Please hold while I connect you to our team", the US number rang, and the answering side said "Hello" in a two-way conversation. That is a US-to-US Dial, the production handoff path to a US shop owner. It does not prove a direct Dial to a Vietnam number, which remains blocked on a trial account by the High Risk:IRSF permission gate (Twilio error 13227); that Vietnam path is not claimed.
6. **Lead capture** (`LeadService`, dashboard API): a lead is created on turn 1 and enriched progressively as fields are captured; `LeadStatus.Booked` requires Address + PreferredTime. This repo does **not** claim a lead is saved only when all 4 fields are present.

## What is NOT in this repo

- No SMS webhook, no SMS use case, no SMS outbound service. Those were removed from this copy: `InboundSmsFunction`, `HandleInboundSms*`, `TwilioSmsService`, `IOutboundSmsService`, and the SMS tests are absent. The voice path does not depend on them.
- No email function, no gap-analysis doc from the original product repo.
- No secrets, no real keys, no real customer data. All test data in the demo is synthetic.

### Naming and legacy data — stated plainly for audit

- Project and namespace names still contain `SmsAi` (`AirwayHVAC.SmsAi.*`). That is a legacy name from the original product; it is not an SMS feature in this repo. Renaming every project/namespace was not done because it would touch every file without changing behaviour, and the audit-relevant question is whether SMS code runs — it does not.
- `ConversationChannel.Sms` remains only as an enum value, for reading pre-existing Cosmos documents whose stored channel string is `"Sms"`. No code path in this repo creates, sends, or answers an SMS. New conversations created by this repo use `ConversationChannel.Voice`.
- The shared conversation/lead/tenant core in this repo predates the window as a data model and is reused by the voice path. The voice-specific work on top of it — webhooks, voice use cases, AI reply layer, tool calling, the 4-tier Meta fallback chain, and the Confirm/Completed gating — was built in the window. If the organizers rule that the shared core must also live outside this repo, that is the one open question in the submission thread; the SMS feature separation itself is complete.

## Meta models used

All runtime model tiers are Meta models. Tier 1 is the Meta Model API; tiers 2–4 are Muse Spark Open via partner inference:

| Tier | Model | Provider / endpoint |
|---|---|---|
| 1 | `muse-spark-1.3` | Meta Model API, https://api.meta.ai/v1 |
| 2 | `meta-models/muse-spark-open-1026` | Baseten, https://inference.baseten.co/v1 |
| 3 | `accounts/fireworks/models/muse-spark-open` | Fireworks, https://api.fireworks.ai/inference/v1 |
| 4 | `meta-models/Muse-Spark-Open-816B-A42B` | Modal (author's own deployment, OpenAI-compatible) |

Set `AI_PROVIDER=Fallback` to use the chain in order Meta → Baseten → Fireworks → Modal. A tier whose key is missing fails per call and is skipped; it never crashes the call. The answering tier is proven by failure-count elimination in the function-host log (N fallback errors per turn means tier N+1 answered), because every partner leg logs under the same responder label.

Forced-failure evidence (real runs, not slides): Meta 27–28/10, Baseten 28/10 + 30/10, Fireworks 30/10, Modal 30/10 (after fixing Modal's rejection of `reasoning_effort=minimal`; Modal runs at `medium`). Baseten credit runs to 9 November, but the Baseten hackathon key itself is valid only 7 days from its email date; on that basis we do not claim Baseten fallback cover for judging week 5–9 November, and the judging-week fallback rests on the Fireworks and Modal Spark Open tiers, whose key expiry is likewise unconfirmed. The chain survives on any one partner tier.

Voice note: the spoken voice heard on a call is Twilio text-to-speech reading the AI's reply text; it is not a model call. All model calls in the chain above are Meta models (Meta Model API or Muse Spark Open via a partner).

Open Weight note, stated transparently: Muse Spark Open is the fallback path here, not tier 1. This architecture was built on Discord #questions 31/10 09:08, which said a hybrid with Spark Open carrying real production load counts for Best Build on Meta Open Weight Models. A newer answer, Discord #questions 03/11 07:52, said a hybrid only counts when the main runtime model itself is a Meta open-weight model. That newer answer arrived one day before the submission deadline, too close to safely switch the main tier, retest, and re-film without risking the core submission. We keep Baseten as fallback, present the fallback evidence honestly, and do not claim Spark Open is the main tier; the Open Weight entry is kept so no chance is missed, and the core Startup-track and Use of Meta AI Tooling claims stand unchanged either way.

Note on Modal: the default Modal endpoint in code is the author's own deployment hostname. It is auth-gated and useless without the author's proxy token (`MODAL_PROXY_TOKEN_ID` + `MODAL_PROXY_TOKEN_SECRET`, composed as `id.secret`). Modal rejects `reasoning_effort=minimal` (HTTP 400, empty body); the default in this repo is `medium`.

## Judges-run path

### With your own keys (full live path)

Requirements: .NET SDK 8.0+, Azure Functions Core Tools 4+, a Twilio number,
a Cosmos DB account, and API keys for Meta / Baseten / Fireworks / Modal
(see the sample `local.settings.json` in Configuration below — no real values
in this repo).

```bash
dotnet build AirwayHVAC.SmsAi.sln
cd src/AirwayHVAC.SmsAi.Api
func start
# Host: http://localhost:7071
# Webhook: POST http://localhost:7071/api/webhooks/twilio/voice/inbound
```

Use one source for each key. The `.env.example` file is only a template of
environment-variable names; the Functions host does not auto-load a `.env`
file. Put your own values in `local.settings.json` (git-ignored, never
commit it) as shown in Configuration, or export them in your shell. Do not
set the same key in both places with different values: a key already set in
your shell wins over the same key in `local.settings.json` for the mapped
keys in `Program.cs` (observed 02/11, when a shell value caused the
`local.settings.json` value to be skipped).

If `AzureWebJobsStorage` is `UseDevelopmentStorage=true`, start Azurite
first. Otherwise use a real storage connection string for that key.

Seed one synthetic tenant in Cosmos DB before testing. Database: `hvac-saas`
(or your `COSMOS_DATABASE`). Containers: `tenants`, `conversations`, `leads`,
`events`, `voiceSessions`. Tenant document for the `tenants` container:

```json
{
  "id": "test-tenant-1",
  "tenantId": "test-tenant-1",
  "companyName": "Test HVAC Services",
  "tenantName": "Test HVAC Services",
  "phoneNumber": "+15550009999",
  "serviceArea": "Dallas, Texas",
  "timezone": "America/Chicago"
}
```

Test credentials (sample only, no real values): set
`TENANT_ROUTING_VERIFICATION_KEY` on the host to your own test value, for
example `test-verification-key`, and send it back as the `X-Tenant-Key`
header with `X-Tenant-Id: test-tenant-1`. Without a matching verification
key the tenant header is rejected. This verified header is the test path;
do not rely on a default-tenant fallback, because `Program.cs` maps no
environment variable for `AllowDefaultTenantFallback` and that flag defaults
to `false`. The `To` number below is only a placeholder in header routing;
the tenant comes from the verified header, not from `To`.

Inbound call (expect HTTP 200 with a `<Gather>` greeting):

```bash
curl -X POST http://localhost:7071/api/webhooks/twilio/voice/inbound \
  -H "X-Tenant-Id: test-tenant-1" \
  -H "X-Tenant-Key: test-verification-key" \
  --data-urlencode "From=+15550001111" \
  --data-urlencode "To=+15550009999" \
  --data-urlencode "CallSid=CAtest00000000000000000000000000"
```

Gather turns, same `CallSid` each time (on PowerShell, use `curl.exe`):

```bash
# Issue -> Address
curl -X POST http://localhost:7071/api/webhooks/twilio/voice/gather \
  -H "X-Tenant-Id: test-tenant-1" \
  -H "X-Tenant-Key: test-verification-key" \
  --data-urlencode "CallSid=CAtest00000000000000000000000000" \
  --data-urlencode "SpeechResult=AC not cooling" \
  --data-urlencode "Confidence=0.95"

# Address -> Urgency: SpeechResult=123 Main St
# Urgency -> Appointment: SpeechResult=emergency
# Appointment -> Confirm: SpeechResult=tomorrow 9am
# Confirm -> Completed: SpeechResult=yes (expect Hangup)
```

Separate handoff call, new `CallSid` (expect `Dial` to the
`TWILIO_VOICE_HANDOFF_NUMBER` configured on the host):

```bash
curl -X POST http://localhost:7071/api/webhooks/twilio/voice/inbound \
  -H "X-Tenant-Id: test-tenant-1" \
  -H "X-Tenant-Key: test-verification-key" \
  --data-urlencode "From=+15550001111" \
  --data-urlencode "To=+15550009999" \
  --data-urlencode "CallSid=CAtest00000000000000000000000001"
curl -X POST http://localhost:7071/api/webhooks/twilio/voice/gather \
  -H "X-Tenant-Id: test-tenant-1" \
  -H "X-Tenant-Key: test-verification-key" \
  --data-urlencode "CallSid=CAtest00000000000000000000000001" \
  --data-urlencode "SpeechResult=representative" \
  --data-urlencode "Confidence=0.95"
```

Warm up the host with a throwaway call first: a cold first turn can exceed
Twilio's roughly 15-second voice webhook ceiling (warm gather turns run 5–8 seconds).

### Without external accounts (replay path)

Judges without Twilio/Cosmos/API accounts can still evaluate the core from this repo alone:

- Run `dotnet test` for the voice, tool-loop, fallback, and stage-gate tests.
- Read the recorded live-call evidence referenced in the submission (video, host logs, lead records). Recorded outputs are labelled as recorded; they are not presented as a live run by the judge.
- The demo video shows the product actually running (a real call), not slides. Slow segments may be sped up in editing, as permitted.

## Demo video

3 minutes, in English (or with English subtitles/transcript). First line discloses pre-existing work: the SMS part predates the window; the call part shown here was built 26 October – 4 November 2026. The handoff shot is included because a real ringing Dial call passed 02/11/2026 (see Human handoff above); it is never faked. The demo call is filmed on the trial number with only the trial-message segment at the start cut in editing; the disclosure, greeting, and full real call are kept.

## Configuration

Create `src/AirwayHVAC.SmsAi.Api/local.settings.json` from the sample below and
replace every `replace-with-*` value with a credential from your own test
account. These are placeholders, not working secrets. Do not commit this file;
it is git-ignored and must never be added to a submission commit.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AI_PROVIDER": "Fallback",

    "TWILIO_SID": "replace-with-twilio-test-account-sid",
    "TWILIO_TOKEN": "replace-with-twilio-test-auth-token",
    "TWILIO_NUMBER": "+15005550006",
    "TWILIO_VOICE_PUBLIC_BASE_URL": "https://replace-with-your-ngrok-host",
    "TWILIO_VOICE_HANDOFF_NUMBER": "+15551234567",
    "TWILIO_VOICE_DISCLOSURE_MESSAGE": "This call is handled by an AI assistant.",

    "META_BASE_URL": "https://api.meta.ai/v1",
    "META_API_KEY": "replace-with-meta-api-key",
    "META_MODEL": "muse-spark-1.3",
    "META_REASONING_EFFORT": "minimal",

    "BASETEN_BASE_URL": "https://inference.baseten.co/v1",
    "BASETEN_API_KEY": "replace-with-baseten-api-key",
    "BASETEN_MODEL": "meta-models/muse-spark-open-1026",
    "BASETEN_REASONING_EFFORT": "low",

    "FIREWORKS_BASE_URL": "https://api.fireworks.ai/inference/v1",
    "FIREWORKS_API_KEY": "replace-with-fireworks-api-key",
    "FIREWORKS_MODEL": "accounts/fireworks/models/muse-spark-open",

    "MODAL_BASE_URL": "replace-with-modal-openai-compatible-url",
    "MODAL_PROXY_TOKEN_ID": "replace-with-modal-token-id",
    "MODAL_PROXY_TOKEN_SECRET": "replace-with-modal-token-secret",
    "MODAL_MODEL": "meta-models/Muse-Spark-Open-816B-A42B",
    "MODAL_REASONING_EFFORT": "medium",

    "COSMOS_ENDPOINT": "https://replace-with-account.documents.azure.com:443/",
    "COSMOS_KEY": "replace-with-cosmos-key",
    "COSMOS_DATABASE": "hvac-saas",

    "TENANT_ROUTING_VERIFICATION_KEY": "replace-with-local-test-verification-key"
  }
}
```

What each credential group does:

| Group | Purpose |
|---|---|
| `AzureWebJobsStorage`, `FUNCTIONS_WORKER_RUNTIME` | Connect the local Functions host to Azurite and select the .NET isolated worker. Start Azurite first when using `UseDevelopmentStorage=true`. |
| `TWILIO_*` | Authenticate Twilio, identify the voice number, build public callback URLs, and choose the number used for human handoff. Twilio test credentials validate API requests but do not place a real phone call; use a voice-capable account and number for an end-to-end call. |
| `META_*` | Enable the primary Meta Model API tier. `META_API_KEY` is also the only external credential needed by the live-provider unit test. |
| `BASETEN_*` | Enable fallback tier 2. `BASETEN_REASONING_EFFORT=low` is the supported local setting for this model. |
| `FIREWORKS_*` | Enable fallback tier 3. |
| `MODAL_*` | Enable fallback tier 4 against an OpenAI-compatible Modal deployment; both proxy token values are required. If you leave `MODAL_BASE_URL` blank, the code default (the author's deployment hostname) applies, and the tier fails safely per call without the author's token. |
| `COSMOS_*` | Connect the app to the database that stores tenants, conversations, leads, events, and voice sessions. |
| `TENANT_ROUTING_*` | Verify an explicit tenant header (`X-Tenant-Id` + `X-Tenant-Key`). There is no default-tenant fallback in the test path: `AllowDefaultTenantFallback` defaults to `false` and `Program.cs` maps no environment variable for it. |

For tests that do not call an external provider, no real Twilio or Cosmos
credential is needed. Run them with `dotnet test AirwayHVAC.SmsAi.sln`. To run
the live Meta test, set `META_API_KEY` (and optionally `META_BASE_URL` and
`META_MODEL`) in the process, then run:

```bash
dotnet test AirwayHVAC.SmsAi.sln --filter LiveProvider
```
