# Wispr Flow: Detailed Software Report

## Company Overview

Wispr is a voice AI company founded in 2021 and headquartered in San Francisco [^6]. Its flagship product, **Wispr Flow**, is an AI-powered voice-to-text dictation platform that converts natural speech into clean, polished, formatted text across any application [^2]. The company was co-founded by CEO Tanay Kothari and CTO Sahaj Garg after experiencing the frustration of typing-intensive workflows [^6]. Wispr has raised a total of $81 million across all funding rounds and is valued at $700 million post-money as of November 2025, with investors including Notable Capital, Menlo Ventures, NEA, 8VC, and Flight Fund [^25][^28].

The product has achieved significant traction: users dictate over 1 billion words per month, the user base has grown 100x year-over-year with 70% retention over 12 months, and the company has reached 270 of the Fortune 500 [^28][^20]. After six months, a typical user generates 72% of their characters through Flow across nearly 70 apps and websites [^31].

---

## Core Software Architecture

### Speech Processing Pipeline

Wispr Flow's software operates as a multi-step AI inference pipeline with three primary layers [^23]:

1. **Automatic Speech Recognition (ASR)**: Custom-built, context-aware ASR models that transcribe spoken audio into raw text. These models are conditioned on speaker qualities, surrounding context (such as which app the user is in), and individual dictation history [^20].
2. **LLM-Based Transcript Enhancement**: Fine-tuned Meta Llama models clean up, format, and contextualize the raw transcription. This step removes filler words, applies punctuation, structures lists, corrects grammar, and matches the user's personal writing style [^23].
3. **Output Delivery**: The processed text is inserted directly into whatever text field the user is currently focused on, across any application [^8].

### Infrastructure & Performance

The entire pipeline—from speech recognition through LLM enhancement—runs end-to-end in **under 700 milliseconds at p99 latency** (meaning 99 out of 100 requests complete within this window) [^23]. Specifically:

- ASR inference: <200ms
- LLM inference: <200ms
- Networking budget: <200ms (globally, including spotty connections) [^20]

The Llama model must consistently process and generate 100+ tokens in under 250 milliseconds, achieved using Baseten's TensorRT-LLM engine builder and Chains framework for multi-step inference [^23]. Flow runs on **Baseten** for model serving with **AWS** for cloud infrastructure, using dedicated (not shared) GPU deployments for security [^23]. The system uses autoscaling GPUs to handle traffic spikes and scales to zero when not in use [^23].

### Personalization Architecture

A key design constraint is that **most personalization data lives on the user's device** due to privacy requirements [^20]. The system must represent and store context information (dictation history, current app context) locally in a way that can still inform the cloud-based ASR and LLM models during inference. This hybrid architecture balances latency, privacy, and personalization.

---

## Core Functionalities

### 1. Universal Voice Dictation

Flow works in **any text field across any application**—Notion, Gmail, Google Docs, WhatsApp, Slack, VS Code, Cursor, ChatGPT, and more [^8][^3]. Unlike dictation tools tied to specific ecosystems, Flow operates as a system-level overlay on Mac, Windows, and iPhone [^2]. Users activate dictation by holding a hotkey (Fn on Mac, Ctrl+Windows on PC) and speak naturally [^29].

### 2. AI Auto-Edits

Flow goes far beyond raw transcription [^8][^2]:

- **Filler word removal**: Automatically strips "um," "uh," "like," and other verbal filler
- **Smart punctuation**: Adds commas, periods, question marks contextually
- **List formatting**: Recognizes when the user is listing items and formats accordingly
- **Real-time correction handling**: If a user corrects themselves mid-sentence, Flow adapts instantly rather than transcribing the correction literally
- **Backtracking**: Understands when a user restates or revises what they just said

### 3. Command Mode

A Pro-tier feature that enables **voice-based text editing and transformation** [^26][^10]. Users select existing text and issue voice commands to modify it:

- "Make this more formal"
- "Turn this into bullet points"
- "Summarize this"
- "Translate this to Spanish"
- "Expand on this point"

Command Mode essentially gives users an inline AI editor triggered entirely by voice, eliminating the need to copy-paste into ChatGPT or similar tools [^26].

### 4. Personal Dictionary

Flow automatically learns the user's unique vocabulary over time [^8][^2]. When a user corrects a misspelling, Flow adds the correct spelling to a personal dictionary. Users can also manually add industry terms, proper nouns, acronyms, and unique spellings. The dictionary syncs across all devices.

### 5. Snippet Library

Users create **voice-triggered shortcuts** for frequently used text blocks [^8]. Speaking a designated cue word inserts the full, pre-formatted text instantly. Use cases include:

- Scheduling links
- FAQ responses
- Email signatures
- Support reply templates
- Bug report templates

### 6. Adaptive Tone/Styles

Flow automatically adjusts its output tone based on which application the user is writing in [^8][^2]. For example, output in Google Docs may be formal and structured, while output in Slack or iMessage is casual and concise. This feature currently works in English on desktop only.

### 7. Contextual Name Spelling

Flow uses surrounding context (the app being used, the content of the conversation) to correctly spell uncommon names, product names, and proper nouns without manual correction [^8].

### 8. Whisper Mode

Flow can recognize and transcribe **sub-audible speech** (whispering), allowing users to dictate in shared or quiet environments like open offices, libraries, or meetings [^8][^6]. The company acknowledges this is a technically challenging problem since no existing ASR systems were specifically trained for whispered speech [^20].

### 9. Multi-Language Support

Flow supports **100+ languages** with automatic language detection [^8][^2]. It can handle multilingual code-switching—using multiple languages in the same sentence—which is a frontier challenge the team continues to refine [^20].

### 10. Cross-Device Sync

The personal dictionary, snippets, notes, and settings sync seamlessly between Mac, Windows, and iPhone [^2][^7]. The subscription is account-based, not device-based.

### 11. Notes

A built-in note-taking feature launched in 2025 [^7]. Users press Cmd+N to create a new note that benefits from all of Flow's transcription quality, dictionary, and formatting capabilities. Notes sync cross-platform, enabling users to start a note on their phone and finish on desktop.

---

## Developer-Specific Capabilities

Wispr Flow has invested heavily in making voice dictation work for software development workflows [^16][^19]:

- **Syntax awareness**: Recognizes and correctly formats camelCase, snake_case, and developer acronyms
- **Developer term recognition**: Handles terms like Supabase, MongoDB, Vercel, etc. without misspelling
- **File tagging**: In Cursor and Windsurf, saying "fix the auth bug in authCheck.ts" automatically tags the referenced file, pulling its context into the AI prompt [^14]
- **Variable recognition**: Saying "set isLoginError to false" is correctly interpreted as a code variable assignment [^14]
- **PR summaries and documentation**: Dictate pull request descriptions, design decisions, release notes, and commit messages hands-free [^16]
- **IDE integration**: Works natively in VS Code, Cursor, Windsurf, Warp Terminal, and Replit [^6][^16]
- **Vibe coding**: Users speak natural language prompts and Flow translates them into structured prompts for AI coding tools [^19]

The partnership with Warp terminal enables developers to speak commands and questions directly into the AI terminal's Agent Mode [^6].

---

## Team & Enterprise Features

### Collaboration Tools

- **Shared dictionary**: Teams can maintain a unified dictionary of product names, jargon, acronyms, and team member names [^8]
- **Shared snippets**: Team-wide voice shortcuts for common replies, links, and templates [^8]
- **Usage dashboards**: Admin-facing analytics showing total words dictated, top apps used, and adoption trends [^8]
- **Centralized billing and admin controls** [^18]

### Enterprise Security & Compliance

| Feature | Basic (Free) | Pro ($15/mo) | Enterprise (Custom) |
|---|---|---|---|
| Privacy Mode / ZDR | ✅ (optional) | ✅ (optional) | ✅ (enforced org-wide) |
| HIPAA-ready (with BAA) | ✅ | ✅ | ✅ (enforced org-wide) |
| SOC 2 Type II | ❌ | ❌ | ✅ |
| ISO 27001 | ❌ | ❌ | ✅ |
| SSO / SAML | ❌ | ❌ | ✅ |
| Advanced usage dashboards | ❌ | ❌ | ✅ |
| MSA & DPA | ❌ | ❌ | ✅ |
| Dedicated support | ❌ | ❌ | ✅ |

[^18][^21]

### Privacy Mode (Zero Data Retention)

When enabled, **no transcript data is stored on Wispr's servers** and no data is used for model training by Wispr or any third party [^24][^27]. Transcript data is stored only locally on the user's device. When a user signs out, local data is deleted. Enterprise admins can enforce Privacy Mode organization-wide and set auto-deletion of local history on a daily basis [^24].

### Enterprise API

Wispr offers an Enterprise API that allows businesses to embed Flow's voice-to-text capabilities directly into their own internal systems, workflows, and AI-powered applications [^6].

---

## Pricing Structure

| Plan | Price | Key Limits |
|---|---|---|
| Flow Basic | Free | 2,000 words/week (desktop), 1,000 words/week (mobile) |
| Flow Pro | $15/month ($12/month annual) | Unlimited words, Command Mode, team features |
| Flow Enterprise | Custom pricing | All Pro features + enforced security, SSO, compliance |

All new accounts receive a 14-day free trial of Pro with no credit card required [^18].

---

## Platform Availability

- **macOS**: Full-featured desktop app (original platform)
- **Windows**: Full-featured desktop app launched March 2025 [^6]
- **iOS (iPhone)**: Voice keyboard app available on App Store [^12]
- **Android**: Not yet available; waitlist open [^18]

---

## Technical Challenges & R&D Roadmap

Wispr has publicly documented the frontier technical problems they are actively solving [^20]:

- **Personalized LLM formatting**: Achieving token-level control over output formatting (dashes vs. commas, capitalization preferences) while maintaining LLM precision
- **Learning from corrections**: Building local reinforcement learning policies that align with individual user style preferences so the system never makes the same mistake twice
- **Multilingual code-switching**: Accurately transcribing utterances that mix multiple languages in a single sentence
- **Communicating uncertainty**: Developing UX and modeling approaches to signal when output should be reviewed vs. trusted immediately
- **Scale**: Processing 1 billion words/month with 99.99% uptime at ultra-low latency, with expected 10x growth in the near term
- **Hardware for voice everywhere**: Planned for late 2026 at earliest—designing hardware form factors for using voice interfaces around other people

The company's long-term vision extends beyond dictation: they aim to build a voice interface that can both **do things for users** and **proactively help them**, positioning speech as a primary computing modality [^20][^31].

---

## Integration Ecosystem

Flow integrates with or works inside the following platforms [^8][^16][^6][^1]:

- **Productivity**: Notion, Google Docs, Gmail, Slack, Microsoft Teams, WhatsApp, iMessage
- **Development**: VS Code, Cursor, Windsurf, Warp Terminal, Replit, GitHub
- **AI Tools**: ChatGPT, Claude, Perplexity
- **Project Management**: Jira
- **Other**: Any application with a text field (system-level integration)

Native integrations allow actions like "Ask Perplexity, what does this mean?" directly from selected text [^6].

---

## Summary of Core Software Components

| Component | Technology / Approach |
|---|---|
| Speech Recognition | Custom context-aware ASR models (speaker-conditioned, personalized) |
| Text Enhancement | Fine-tuned Meta Llama LLMs with token-level formatting control |
| Inference Serving | Baseten (TensorRT-LLM, Chains framework) on dedicated AWS GPU deployments |
| Latency Target | <700ms end-to-end at p99 |
| Personalization Storage | On-device (local-first architecture for privacy) |
| Security Certifications | SOC 2 Type II, ISO 27001, HIPAA |
| Platforms | macOS, Windows, iOS |
| Languages | 100+ with automatic detection and code-switching |


---

## References

1. [Developer Documentation Tools: Boosting Productivity with ...](https://wisprflow.ai/post/developer-documentation-tools) - Flow can document functions and variables as developers describe them, making it easy to capture det...

2. [Wispr Flow | Effortless Voice Dictation](https://wisprflow.ai) - Flow makes writing quick and clear with seamless voice dictation. It is the fastest, smartest way to...

3. [Why is Wispr Flow different from other dictation apps?](https://zapier.com/blog/wispr-flow/) - Wispr Flow is an iOS and desktop app that offers fast, accurate dictation across all your apps with ...

6. [Developers are Ditching their Keyboards as Wispr Flow Expands to ...](https://www.prnewswire.com/news-releases/developers-are-ditching-their-keyboards-as-wispr-flow-expands-to-new-platforms-302399506.html) - Wispr Flow now supports both Mac and Windows, unlocking new possibilities for professionals, creator...

7. [Wispr Flow: What's New](https://roadmap.wisprflow.ai) - Flow Pro: for individual users who need unlimited dictation, priority support, and/or early access t...

8. [Features - Wispr Flow](https://wisprflow.ai/features) - Flow goes beyond basic dictation: cleaning up filler words, formatting lists, catching punctuation, ...

10. [A complete Wispr Flow overview for 2025 - eesel AI](https://www.eesel.ai/blog/wispr-flow-overview) - Our comprehensive Wispr Flow overview covers its key features, pricing, use cases, and significant u...

12. [Wispr Flow: AI Voice Keyboard - App Store - Apple](https://apps.apple.com/ca/app/wispr-flow-ai-voice-keyboard/id6497229487) - Talk naturally. Flow writes perfectly. Trusted by business leaders, creatives, students, and profess...

14. [Wispr Flow - LinkedIn](https://www.linkedin.com/company/wisprflow) - Wispr Flow (available on Mac, iPhone and Windows) lets you speak naturally and see your words perfec...

16. [Dictation built for developers](https://wisprflow.ai/developers) - Built for voice-first workflows, it understands dev jargon, works across your favorite tools, and wo...

18. [Flow plans and what's included](https://docs.wisprflow.ai/articles/9559327591-flow-plans-and-what-s-included) - Feature. Flow Basic. Flow Pro. Flow Enterprise. Price. Free. $15/month or $12/month (annual). Contac...

19. [AI Tools for Developers: Vibe Coding with Voice to Ship ...](https://wisprflow.ai/post/developer-tools) - With Wispr Flow, you can write code hands-free using only your voice, making coding more accessible ...

20. [Technical Challenges Behind Flow](https://wisprflow.ai/post/technical-challenges) - At Wispr Flow, we are building: The world's best ASR models (context aware, personalized, and code-s...

21. [Flow for Business](https://wisprflow.ai/business) - Plans & pricing. Monthly. Annual. 20% discount. For individuals. Flow Basic. Free ... Talk to us abo...

23. [Wispr Flow creates effortless voice dictation with Llama on ...](https://www.baseten.co/resources/customers/wispr-flow/) - Wispr Flow runs fine-tuned Llama models with Baseten and AWS to provide seamless dictation across ev...

24. [Wispr Flow IT guide on privacy and security](https://docs.wisprflow.ai/articles/1163060507-wispr-flow-it-guide-on-privacy-and-security) - Zero data retention (aka Privacy Mode) ... Privacy Mode means that no transcript data is stored on o...

25. [Wispr valuation, funding & news | Sacra](https://sacra.com/c/wispr/) - Wispr closed a $25 million Series A extension in November 2025 led by Notable Capital, bringing the ...

26. [New Command Mode in Wispr Flow](https://www.youtube.com/watch?v=73iCBZhYye8) - Now they introduce command mode and this actually lets you edit or transform text after you've dicta...

27. [Privacy | Wispr Flow](https://wisprflow.ai/privacy) - What is Privacy Mode and how does it work? Privacy Mode is a setting in Settings → Data & Privacy th...

28. [As its voice dictation app takes off, Wispr secures $25 ... - TechCrunch](https://techcrunch.com/2025/11/20/as-its-voice-dectation-app-takes-off-wispr-secures-25m-from-notable-capital/) - With this influx of capital, the company has raised $81 million in total. Sources told TechCrunch th...

29. [Wispr Flow Tutorial for Beginners 2026 (Step By Step)](https://www.youtube.com/watch?v=qlg3p5HdXRQ) - I'll walk you through how Flow works, show real world examples and share simple tips so you can use ...

31. [Wispr Raises $25M To Build Its Voice Operating System](https://finance.yahoo.com/news/wispr-raises-25m-build-voice-160000047.html) - Round Led by Notable Capital With Participation From Flight Fund; Brings Total Funding to $81M. SAN ...

