#!/usr/bin/env bash
set -euo pipefail

# ─── MOTO Editor - Suite de tests E2E (Linux/macOS) ───

OUTPUT_DIR="${1:-./test-reports}"
SKIP_STRESS_TESTS="${2:-false}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

log_header() { echo -e "\n${CYAN}🚀 $1${NC}"; }
log_ok()     { echo -e "   ${GREEN}✅ $1${NC}"; }
log_warn()   { echo -e "   ${YELLOW}⚠️  $1${NC}"; }
log_err()    { echo -e "   ${RED}❌ $1${NC}"; }

START_TIME=$(date +%s)

# ─── Vérification des prérequis ───
log_header "Vérification des prérequis"

if ! command -v dotnet &> /dev/null; then
    log_err ".NET SDK non installé. Installez-le depuis https://dotnet.microsoft.com/download"
    exit 1
fi
log_ok ".NET SDK: $(dotnet --version)"

mkdir -p "$OUTPUT_DIR"
log_ok "Répertoire de sortie: $OUTPUT_DIR"

# ─── Informations système ───
log_header "Collecte des informations système"

OS_NAME=$(uname -s)
OS_VERSION=$(uname -r)
CPU_INFO=$(sysctl -n machdep.cpu.brand_string 2>/dev/null || cat /proc/cpuinfo | grep "model name" | head -1 | cut -d: -f2 | xargs)
CPU_CORES=$(nproc 2>/dev/null || sysctl -n hw.ncpu)
RAM_TOTAL_GB=$(free -g | awk '/^Mem:/{print $2}' 2>/dev/null || sysctl -n hw.memsize | awk '{print int($1/1073741824)}')

cat > "$OUTPUT_DIR/machine-info.json" <<EOF
{
  "hostname": "$(hostname)",
  "os": "$OS_NAME",
  "os_version": "$OS_VERSION",
  "cpu": "$CPU_INFO",
  "cpu_cores": $CPU_CORES,
  "ram_total_gb": $RAM_TOTAL_GB,
  "dotnet_version": "$(dotnet --version)",
  "test_date": "$(date '+%Y-%m-%d %H:%M:%S')"
}
EOF
log_ok "Machine: $(hostname) | RAM: ${RAM_TOTAL_GB}GB | CPU: $CPU_CORES cores"

# ─── Tests E2E ───
log_header "Exécution des tests E2E (Moto.Tests)"

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

E2E_FILTERS=(
    "ModelCorruptionTests"
    "DownloadInterruptionTests"
    "CircuitBreakerTests"
)

for filter in "${E2E_FILTERS[@]}"; do
    echo "   → $filter"
    TOTAL_TESTS=$((TOTAL_TESTS + 1))

    if dotnet test Moto.Tests/Moto.Tests.csproj \
        --filter "FullyQualifiedName~$filter" \
        --logger "trx;LogFileName=$OUTPUT_DIR/$filter.trx" \
        --results-directory "$OUTPUT_DIR" \
        > /dev/null 2>&1; then
        log_ok "$filter: PASSED"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        log_err "$filter: FAILED"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
done

# ─── Stress tests ───
if [ "$SKIP_STRESS_TESTS" != "true" ]; then
    log_header "Stress tests mémoire/CPU"

    STRESS_DURATION=300
    echo "   Durée: ${STRESS_DURATION}s"

    # Lance le stress test en arrière-plan
    dotnet run --project Moto.Editor/Moto.Editor.csproj \
        --configuration Release \
        -- --stress-test --duration $STRESS_DURATION \
        > "$OUTPUT_DIR/stress-stdout.log" 2>&1 &
    STRESS_PID=$!

    # Surveillance mémoire
    MEM_SAMPLES_FILE="$OUTPUT_DIR/stress-memory-samples.csv"
    echo "timestamp,working_set_mb,cpu_percent" > "$MEM_SAMPLES_FILE"

    for i in $(seq 0 5 $STRESS_DURATION); do
        if kill -0 $STRESS_PID 2>/dev/null; then
            MEM_MB=$(ps -o rss= -p $STRESS_PID 2>/dev/null | awk '{print int($1/1024)}' || echo "0")
            echo "$(date '+%Y-%m-%d %H:%M:%S'),$MEM_MB,0" >> "$MEM_SAMPLES_FILE"
        fi
        sleep 5
    done

    kill $STRESS_PID 2>/dev/null || true
    log_ok "Stress test terminé."
fi

# ─── Benchmark par tier ───
log_header "Benchmark tokens/s par tier"

BENCHMARK_CSV="$OUTPUT_DIR/benchmark-summary.csv"
echo "tier,tokens_per_second,ram_usage_mb,latency_ms" > "$BENCHMARK_CSV"

for tier in lite standard full; do
    echo "   → Tier: $tier"

    if dotnet run --project Moto.Editor/Moto.Editor.csproj \
        --configuration Release \
        -- --benchmark --tier $tier --output "$OUTPUT_DIR/benchmark-$tier.json" \
        > /dev/null 2>&1; then

        if [ -f "$OUTPUT_DIR/benchmark-$tier.json" ]; then
            TPS=$(jq -r '.tokens_per_second // 0' "$OUTPUT_DIR/benchmark-$tier.json")
            RAM=$(jq -r '.ram_usage_mb // 0' "$OUTPUT_DIR/benchmark-$tier.json")
            LAT=$(jq -r '.latency_ms // 0' "$OUTPUT_DIR/benchmark-$tier.json")
            echo "$tier,$TPS,$RAM,$LAT" >> "$BENCHMARK_CSV"
            log_ok "Tier $tier: $TPS tokens/s"
        fi
    else
        log_warn "Tier $tier: benchmark non disponible"
    fi
done

# ─── Rapport consolidé ───
log_header "Génération du rapport consolidé"

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

STATUS="PASS"
[ "$FAILED_TESTS" -gt 0 ] && STATUS="FAIL"

cat > "$OUTPUT_DIR/consolidated-report.json" <<EOF
{
  "report_type": "MOTO_E2E_Trial_Period",
  "generated_at": "$(date '+%Y-%m-%d %H:%M:%S')",
  "duration_seconds": $DURATION,
  "tests": {
    "total": $TOTAL_TESTS,
    "passed": $PASSED_TESTS,
    "failed": $FAILED_TESTS
  },
  "summary": {
    "status": "$STATUS"
  }
}
EOF
log_ok "Rapport consolidé: $OUTPUT_DIR/consolidated-report.json"

# ─── Résumé ───
log_header "Résumé"
echo "   Durée totale: ${DURATION}s"
echo "   Tests: $PASSED_TESTS/$TOTAL_TESTS passed"

if [ "$FAILED_TESTS" -eq 0 ]; then
    echo -e "\n${GREEN}✅ Suite E2E: TOUTES LES ÉPREUVES RÉUSSIES${NC}\n"
    exit 0
else
    echo -e "\n${RED}❌ Suite E2E: $FAILED_TESTS ÉCHEC(S)${NC}\n"
    exit 1
fi
