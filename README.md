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

Forced-failure evidence (real runs, not slides): Meta 27–28/10, Baseten 28/10 + 30/10, Fireworks 30/10, Modal 30/10 (after fixing Modal's rejection of `reasoning_effort=minimal`; Modal runs at `medium`). Baseten's partner representative confirmed the Baseten key runs until 9 November 2026, covering judging week 5–9 November. No equivalent confirmation exists yet for the Fireworks or Modal keys; the chain survives on any one partner tier.

Note on Modal: the default Modal endpoint in code is the author's own deployment hostname. It is auth-gated and useless without the author's proxy token (`MODAL_PROXY_TOKEN_ID` + `MODAL_PROXY_TOKEN_SECRET`, composed as `id.secret`). Modal rejects `reasoning_effort=minimal` (HTTP 400, empty body); the default in this repo is `medium`.

## Judges-run path

### With your own keys (full live path)

Requirements: .NET SDK 8.0+, Azure Functions Core Tools 4+, a Twilio number, a Cosmos DB account, and API keys for Meta / Baseten / Fireworks / Modal (see `.env.example` — no real values in this repo).

```bash
dotnet build AirwayHVAC.SmsAi.sln
# Run the Functions host, point a Twilio voice webhook at
# POST /api/webhooks/twilio/voice/inbound, then call the number.
# Set AI_PROVIDER=Fallback and the provider keys as environment variables.
```

Warm up the host with a throwaway call first: a cold first turn can exceed Twilio's roughly 15-second voice webhook ceiling (warm gather turns run 5–8 seconds).

### Without external accounts (replay path)

Judges without Twilio/Cosmos/API accounts can still evaluate the core from this repo alone:

- Run `dotnet test` for the voice, tool-loop, fallback, and stage-gate tests.
- Read the recorded live-call evidence referenced in the submission (video, host logs, lead records). Recorded outputs are labelled as recorded; they are not presented as a live run by the judge.
- The demo video shows the product actually running (a real call), not slides. Slow segments may be sped up in editing, as permitted.

## Demo video

3 minutes, in English (or with English subtitles/transcript). First line discloses pre-existing work: the SMS part predates the window; the call part shown here was built 26 October – 4 November 2026. The handoff shot is included because a real ringing Dial call passed 02/11/2026 (see Human handoff above); it is never faked. The demo call is filmed on the trial number with only the trial-message segment at the start cut in editing; the disclosure, greeting, and full real call are kept.

## Configuration

Copy `.env.example` to your environment (do not commit real values). Environment variables win over `local.settings.json`. `local.settings.json` is git-ignored and must never contain real keys in a submission.
