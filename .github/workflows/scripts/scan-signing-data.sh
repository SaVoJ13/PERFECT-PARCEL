#!/usr/bin/env bash
# BUG-011: пошук даних підпису Android у папці Library (вона потрапляє в actions/cache).
# Режими: report — знайдені файли видаляються з робочої копії, код 0 (для щойно відновленого кешу);
#         enforce — будь-яка знахідка дає код 1 (запобіжник перед збереженням кешу).
# Вивід: ЛИШЕ кількість і імена файлів. Значення секретів не друкуються ніколи.
set +x
MODE="${1:-enforce}"
DIR="${2:-Library}"
PATTERN_FILE="$(mktemp)"
trap 'rm -f "$PATTERN_FILE"' EXIT
TOTAL=0

if [ ! -d "$DIR" ]; then
  echo "Папки $DIR немає — перевіряти нічого."
  exit 0
fi

handle_files() { # $1 — мітка, вміст stdin — імена файлів
  local label="$1" files count
  files="$(cat)"
  if [ -z "$files" ]; then count=0; else count=$(printf '%s\n' "$files" | wc -l); fi
  echo "[$label] файлів зі збігом: $count"
  if [ "$count" -gt 0 ]; then
    printf '%s\n' "$files" | sed 's/^/    - /'
    TOTAL=$((TOTAL + count))
    if [ "$MODE" = "report" ]; then
      printf '%s\n' "$files" | while IFS= read -r f; do sudo rm -f -- "$f"; done
      echo "    (видалено з робочої копії; запис кешу в GitHub не змінено)"
    fi
  fi
}

for NAME in ANDROID_KEYSTORE_PASS ANDROID_KEYALIAS_NAME ANDROID_KEYALIAS_PASS; do
  VALUE="${!NAME}"
  if [ -z "$VALUE" ]; then
    echo "[$NAME] значення не передано (збірка без секретів) — пропущено"
    continue
  fi
  printf '%s' "$VALUE" > "$PATTERN_FILE"
  handle_files "$NAME" < <(sudo grep -rlF -f "$PATTERN_FILE" "$DIR" 2>/dev/null | sort)
done

handle_files "файли ключів" < <(sudo find "$DIR" -type f \( -iname '*.keystore' -o -iname '*.jks' -o -name 'keystore.properties' \) 2>/dev/null | sort)

echo "Разом знахідок: $TOTAL (режим: $MODE)"
if [ "$MODE" = "enforce" ] && [ "$TOTAL" -gt 0 ]; then
  echo "::error::У $DIR знайдено дані підпису. Кеш не можна зберігати: виключіть ці шляхи з кешу."
  exit 1
fi
exit 0
