#include "timer_core/line.h"

#include <stdio.h>
#include <string.h>

/* A valid line has at most a handful of fields; more than this cannot fit a useful line anyway */
#define MAX_FIELDS 8

typedef enum {
    VAL_STRING,
    VAL_INT,   /* integer that fits int32_t */
    VAL_OTHER, /* valid JSON number that is not an int32_t, e.g. 1.5, 1e3 or 3000000000 */
    VAL_TRUE,
    VAL_FALSE,
    VAL_NULL,
} val_kind_t;

typedef struct {
    const char *key;
    size_t key_len;
    val_kind_t kind;
    const char *str;
    size_t str_len;
    int32_t num;
} field_t;

typedef struct {
    const char *p;
    const char *end;
} cursor_t;

static bool is_digit(char ch)
{
    return ch >= '0' && ch <= '9';
}

static void skip_ws(cursor_t *c)
{
    while (c->p < c->end && (*c->p == ' ' || *c->p == '\t' || *c->p == '\r' || *c->p == '\n')) {
        c->p++;
    }
}

static bool accept(cursor_t *c, char ch)
{
    if (c->p < c->end && *c->p == ch) {
        c->p++;
        return true;
    }
    return false;
}

/* String without escapes: the laptop never sends them, and a line that has them is dropped */
static bool parse_string(cursor_t *c, const char **str, size_t *len)
{
    if (!accept(c, '"')) {
        return false;
    }
    const char *start = c->p;
    while (c->p < c->end && *c->p != '"') {
        unsigned char ch = (unsigned char)*c->p;
        if (ch == '\\' || ch < 0x20) {
            return false;
        }
        c->p++;
    }
    if (!accept(c, '"')) {
        return false;
    }
    *str = start;
    *len = (size_t)(c->p - start - 1);
    return true;
}

static bool parse_digits(cursor_t *c)
{
    if (c->p >= c->end || !is_digit(*c->p)) {
        return false;
    }
    while (c->p < c->end && is_digit(*c->p)) {
        c->p++;
    }
    return true;
}

/* JSON number grammar; sets VAL_INT for plain integers in int32_t range, else VAL_OTHER */
static bool parse_number(cursor_t *c, field_t *f)
{
    bool negative = accept(c, '-');
    if (c->p >= c->end || !is_digit(*c->p)) {
        return false;
    }
    if (*c->p == '0' && c->p + 1 < c->end && is_digit(c->p[1])) {
        return false; /* leading zeros are not JSON */
    }

    int64_t value = 0;
    bool in_range = true;
    while (c->p < c->end && is_digit(*c->p)) {
        if (value <= (int64_t)INT32_MAX + 1) {
            value = value * 10 + (*c->p - '0');
        }
        c->p++;
    }
    if (negative) {
        value = -value;
    }
    if (value > INT32_MAX || value < INT32_MIN) {
        in_range = false;
    }

    bool integer = true;
    if (accept(c, '.')) {
        integer = false;
        if (!parse_digits(c)) {
            return false;
        }
    }
    if (accept(c, 'e') || accept(c, 'E')) {
        integer = false;
        if (!accept(c, '+')) {
            accept(c, '-');
        }
        if (!parse_digits(c)) {
            return false;
        }
    }

    f->kind = (integer && in_range) ? VAL_INT : VAL_OTHER;
    f->num = (f->kind == VAL_INT) ? (int32_t)value : 0;
    return true;
}

static bool parse_literal(cursor_t *c, const char *word, val_kind_t kind, field_t *f)
{
    size_t n = strlen(word);
    if ((size_t)(c->end - c->p) < n || memcmp(c->p, word, n) != 0) {
        return false;
    }
    c->p += n;
    f->kind = kind;
    return true;
}

static bool parse_value(cursor_t *c, field_t *f)
{
    if (c->p >= c->end) {
        return false;
    }
    switch (*c->p) {
    case '"':
        f->kind = VAL_STRING;
        return parse_string(c, &f->str, &f->str_len);
    case 't':
        return parse_literal(c, "true", VAL_TRUE, f);
    case 'f':
        return parse_literal(c, "false", VAL_FALSE, f);
    case 'n':
        return parse_literal(c, "null", VAL_NULL, f);
    default:
        /* Numbers; nested objects and arrays are not part of the protocol and fail here */
        return parse_number(c, f);
    }
}

static const field_t *find(const field_t *fields, size_t count, const char *key)
{
    size_t n = strlen(key);
    for (size_t i = 0; i < count; i++) {
        if (fields[i].key_len == n && memcmp(fields[i].key, key, n) == 0) {
            return &fields[i];
        }
    }
    return NULL;
}

/* Flat JSON object with string keys; duplicate keys make the line invalid */
static bool parse_object(cursor_t *c, field_t *fields, size_t *count)
{
    *count = 0;
    skip_ws(c);
    if (!accept(c, '{')) {
        return false;
    }
    skip_ws(c);
    if (!accept(c, '}')) {
        for (;;) {
            if (*count == MAX_FIELDS) {
                return false;
            }
            field_t *f = &fields[*count];
            skip_ws(c);
            if (!parse_string(c, &f->key, &f->key_len)) {
                return false;
            }
            for (size_t i = 0; i < *count; i++) {
                if (fields[i].key_len == f->key_len && memcmp(fields[i].key, f->key, f->key_len) == 0) {
                    return false;
                }
            }
            skip_ws(c);
            if (!accept(c, ':')) {
                return false;
            }
            skip_ws(c);
            if (!parse_value(c, f)) {
                return false;
            }
            (*count)++;
            skip_ws(c);
            if (accept(c, '}')) {
                break;
            }
            if (!accept(c, ',')) {
                return false;
            }
        }
    }
    skip_ws(c);
    return c->p == c->end;
}

static int hex_value(char ch)
{
    if (ch >= '0' && ch <= '9') {
        return ch - '0';
    }
    if (ch >= 'a' && ch <= 'f') {
        return ch - 'a' + 10;
    }
    if (ch >= 'A' && ch <= 'F') {
        return ch - 'A' + 10;
    }
    return -1;
}

static bool parse_color(const field_t *f, uint32_t *color)
{
    if (f->kind != VAL_STRING || f->str_len != 7 || f->str[0] != '#') {
        return false;
    }
    uint32_t value = 0;
    for (size_t i = 1; i < 7; i++) {
        int h = hex_value(f->str[i]);
        if (h < 0) {
            return false;
        }
        value = (value << 4) | (uint32_t)h;
    }
    *color = value;
    return true;
}

bool tc_line_parse(const char *line, size_t len, tc_line_t *out)
{
    if (line == NULL || out == NULL || len + 1 > TC_LINE_MAX_BYTES) {
        return false;
    }
    if (len > 0 && line[len - 1] == '\r') {
        len--;
    }

    field_t fields[MAX_FIELDS];
    size_t count;
    cursor_t c = { line, line + len };
    if (!parse_object(&c, fields, &count)) {
        return false;
    }

    const field_t *v = find(fields, count, "v");
    if (v == NULL || v->kind != VAL_INT || v->num != TC_PROTOCOL_VERSION) {
        return false;
    }

    const field_t *t = find(fields, count, "t");
    const field_t *col = find(fields, count, "c");
    const field_t *len_field = find(fields, count, "L");
    const field_t *state = find(fields, count, "state");
    int discriminators = (t != NULL) + (col != NULL) + (len_field != NULL) + (state != NULL);
    if (discriminators != 1 || t != NULL) {
        return false;
    }

    tc_line_t result = { 0 };
    if (col != NULL) {
        const field_t *d = find(fields, count, "d");
        if (!parse_color(col, &result.color) || d == NULL) {
            return false;
        }
        if (d->kind == VAL_INT) {
            result.has_deviation = true;
            result.deviation_s = d->num;
        } else if (d->kind != VAL_NULL) {
            return false;
        }
        result.type = TC_LINE_STATE;
    } else if (len_field != NULL) {
        if (len_field->kind != VAL_INT || len_field->num <= 0) {
            return false;
        }
        result.type = TC_LINE_LENGTH;
        result.length_s = (uint32_t)len_field->num;
    } else {
        if (state->kind != VAL_STRING || state->str_len != 5 || memcmp(state->str, "ended", 5) != 0) {
            return false;
        }
        result.type = TC_LINE_ENDED;
    }

    *out = result;
    return true;
}

int tc_line_format_status(char *buf, size_t size, uint32_t t, bool run)
{
    if (buf == NULL || size == 0) {
        return -1;
    }
    int written = snprintf(buf, size, "{\"v\":%d,\"t\":%lu,\"run\":%s}\n", TC_PROTOCOL_VERSION,
                           (unsigned long)t, run ? "true" : "false");
    return (written < 0 || (size_t)written >= size) ? -1 : written;
}

static bool version_char_ok(char ch)
{
    return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || ch == '.'
           || ch == '-' || ch == '+' || ch == '_';
}

int tc_line_format_version(char *buf, size_t size, const char *version)
{
    if (buf == NULL || size == 0) {
        return -1;
    }
    char clean[TC_LINE_VERSION_MAX + 1];
    size_t n = 0;
    if (version != NULL) {
        for (; n < TC_LINE_VERSION_MAX && version[n] != '\0'; n++) {
            clean[n] = version_char_ok(version[n]) ? version[n] : '_';
        }
    }
    clean[n] = '\0';
    int written = snprintf(buf, size, "{\"v\":%d,\"fw\":\"%s\"}\n", TC_PROTOCOL_VERSION, n > 0 ? clean : "unknown");
    return (written < 0 || (size_t)written >= size) ? -1 : written;
}
