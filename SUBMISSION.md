# Submission steps (run on your machine)

This local repo is committed and ready to push. The steps below need tools that aren't in the
Cowork sandbox — they require **your** GitHub login and a local **.NET + LEAN** setup.

## 1. Prerequisites
- GitHub CLI: `gh auth status` (run `gh auth login` if not authenticated).
- .NET SDK installed (`dotnet --version`).
- A local clone of the official template **QuantConnect/Lean.DataSource.SDK** — use its
  `.csproj` files as the source of truth for package versions and `TargetFramework`, and merge
  the three `.csproj` files in this repo against them.

## 2. Reconcile project files
Open the official `Lean.DataSource.SDK` `.csproj`, copy its `<TargetFramework>` and QuantConnect
`<PackageReference>`/`<ProjectReference>` entries into:
- `QuantConnect.DataSource.MarketCrunchPredictions.csproj`
- `tests/Tests.csproj`
- `DataProcessing/DataProcessing.csproj`

## 3. Build & test
```bash
dotnet build QuantConnect.DataSource.MarketCrunchPredictions.csproj
dotnet build tests/Tests.csproj
dotnet test  tests/Tests.csproj        # run from repo root
```
Fix any errors until all unit tests pass.

## 4. Create the GitHub repo and push
```bash
git remote remove origin 2>/dev/null || true
gh repo create Lean.DataSource.MarketCrunchPredictions --private --source=. --push
```
This prints the **GitHub repo URL** (e.g. `https://github.com/<your-account>/Lean.DataSource.MarketCrunchPredictions`).

## 5. Upload the full data archive to Dropbox
The repo `output/` holds only sample data. The full dataset archive
(`Lean.DataSource.MarketCrunchPredictions-data.tar.gz`, 246 tickers) goes to Dropbox:
```bash
curl -s -X POST https://content.dropboxapi.com/2/files/upload \
  --header "Authorization: Bearer $DROPBOX_ACCESS_TOKEN" \
  --header "Dropbox-API-Arg: {\"path\": \"/Lean.DataSource.MarketCrunchPredictions-data.tar.gz\", \"mode\": \"overwrite\"}" \
  --header "Content-Type: application/octet-stream" \
  --data-binary @Lean.DataSource.MarketCrunchPredictions-data.tar.gz

curl -s -X POST https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings \
  --header "Authorization: Bearer $DROPBOX_ACCESS_TOKEN" \
  --header "Content-Type: application/json" \
  --data "{\"path\": \"/Lean.DataSource.MarketCrunchPredictions-data.tar.gz\", \"settings\": {\"requested_visibility\": \"public\"}}"
```

## 6. Deliver to QuantConnect
Send Jared the **GitHub repo URL** + the **Dropbox shared-link URL**.

## Notes
- The 5 known data caveats (WMT etc. excluded) and the daily-feed channel are open items to
  confirm with QC — see `../QC_MarketCrunch_Predictions/`.
- If you shared any source/vendor API key with the agent during the build, rotate it (security
  hygiene); it does not affect QC's connection.
