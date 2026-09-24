# StoryIQ story service

See ../RENDER-SETUP.md for production deployment and the Unity connection step.
Node.js 22+; no external dependencies. Run npm test for 13 simulated tests.

For local use, copy .env.example to .env, set OPENAI_API_KEY, then npm start.
Default listener: 127.0.0.1:8787. Local state is stored in ignored data/.
For Render, environment variables replace .env; NODE_ENV=production enables
0.0.0.0 and exact ALLOWED_ORIGINS. DATA_DIR must point to the persistent disk.

POST /v1/story accepts JSON: sessionId and requestId (32-character hex IDs),
expectedTurn (0-20), choiceId (empty for opening, offered choice ID afterward).
Reuse the same requestId when retrying. Replies preserve the Step 2 Unity contract.
GET /health checks server availability only.

Stories end after 20 decisions. Canon, history, response cache and daily generation
allowance persist across restarts. Single instance only. 24-hour idle expiry,
100-session cap. No browser-close save/load or player authentication is included.
Daily generation limit defaults to 200 attempts, shared by all users. It counts
provider failures too, and is not a dollar budget. CORS is not authentication.
Keep .env, data/, and all API keys out of source control and game builds.
