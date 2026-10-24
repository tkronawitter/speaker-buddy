#include "timer_core/session.h"

void tc_session_init(tc_session_t *session)
{
    session->received_any = false;
    session->last_rx_ms = 0;
    session->color = TC_COLOR_BLACK;
    session->has_deviation = false;
    session->deviation_s = 0;
    session->sent_any = false;
    session->last_sent_ms = 0;
    session->last_run = false;
    session->last_t = 0;
}

void tc_session_apply(tc_session_t *session, const tc_line_t *line, tc_clock_t *clock, uint32_t now_ms)
{
    switch (line->type) {
    case TC_LINE_STATE:
        session->color = line->color;
        session->has_deviation = line->has_deviation;
        session->deviation_s = line->deviation_s;
        break;
    case TC_LINE_LENGTH:
        tc_clock_set_length(clock, line->length_s);
        break;
    case TC_LINE_ENDED:
        session->color = TC_COLOR_BLACK;
        session->has_deviation = false;
        session->deviation_s = 0;
        break;
    }
    session->received_any = true;
    session->last_rx_ms = now_ms;
}

bool tc_session_online(const tc_session_t *session, uint32_t now_ms)
{
    return session->received_any && (now_ms - session->last_rx_ms) < TC_LINK_TIMEOUT_MS;
}

void tc_session_link_state(const tc_session_t *session, uint32_t now_ms, tc_link_state_t *out)
{
    out->online = tc_session_online(session, now_ms);
    out->color = session->color;
    out->has_deviation = session->has_deviation;
    out->deviation_s = session->deviation_s;
}

bool tc_session_status_due(tc_session_t *session, const tc_clock_t *clock, uint32_t now_ms, uint32_t *t,
                           bool *run)
{
    uint32_t now_t = tc_clock_seconds(clock, now_ms);
    bool now_run = tc_clock_running(clock);
    bool due = !session->sent_any
               || (now_ms - session->last_sent_ms) >= TC_STATUS_PERIOD_MS
               || now_run != session->last_run
               || now_t < session->last_t;
    if (!due) {
        return false;
    }
    session->sent_any = true;
    session->last_sent_ms = now_ms;
    session->last_run = now_run;
    session->last_t = now_t;
    *t = now_t;
    *run = now_run;
    return true;
}
