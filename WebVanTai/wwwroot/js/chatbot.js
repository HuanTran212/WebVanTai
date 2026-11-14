/**
 * G-Trans AI Assistant Chatbot
 * Hỗ trợ lưu trữ phiên hội thoại (sessionStorage) xuyên suốt các trang / tab
 * Cấu hình bảo mật hoàn toàn ở phía Backend (appsettings.json), không lộ thông tin ra Client
 */
(function () {
    'use strict';

    const STORAGE_KEY_HISTORY = 'gtrans_chat_history_v2';
    const STORAGE_KEY_IS_OPEN = 'gtrans_chat_is_open';

    const state = {
        isOpen: false,
        isLoading: false,
        history: [], // { role: 'user'|'assistant'|'bot', content: string, data?: any, intent?: string, isOpenAi?: boolean, model?: string }
        status: {
            isOpenAiConfigured: false,
            model: 'gpt-4o-mini',
            baseUrl: 'https://api.openai.com/v1',
            isStaff: false,
            userName: null
        }
    };

    // DOM Elements
    let launcherEl, containerEl, bodyEl, inputEl, sendBtnEl, typingEl, statusTextEl, suggestionsContainerEl;

    document.addEventListener('DOMContentLoaded', initChatbot);

    function initChatbot() {
        launcherEl = document.getElementById('chatbotLauncher');
        containerEl = document.getElementById('chatbotContainer');
        bodyEl = document.getElementById('chatbotBody');
        inputEl = document.getElementById('chatbotInput');
        sendBtnEl = document.getElementById('chatbotSendBtn');
        typingEl = document.getElementById('chatbotTyping');
        statusTextEl = document.getElementById('chatbotStatusText');
        suggestionsContainerEl = document.getElementById('chatbotSuggestions');

        if (!launcherEl || !containerEl) return;

        // Gắn sự kiện đóng mở
        launcherEl.addEventListener('click', toggleChatbot);
        document.getElementById('chatbotCloseBtn')?.addEventListener('click', toggleChatbot);
        document.getElementById('chatbotResetBtn')?.addEventListener('click', resetChat);

        // Gửi tin nhắn qua form
        const formEl = document.getElementById('chatbotForm');
        if (formEl) {
            formEl.addEventListener('submit', function (e) {
                e.preventDefault();
                sendMessage();
            });
        }

        // Khôi phục lịch sử chat từ sessionStorage (đảm bảo chuyển tab / chuyển trang vẫn giữ nguyên)
        restoreSessionHistory();

        // Khôi phục trạng thái mở/đóng nếu trước đó khách đang mở
        restoreChatOpenState();

        // Lấy thông tin trạng thái & gợi ý từ server
        fetchStatus();
        fetchSuggestions();
    }

    function restoreSessionHistory() {
        try {
            const saved = sessionStorage.getItem(STORAGE_KEY_HISTORY);
            if (saved) {
                const parsed = JSON.parse(saved);
                if (Array.isArray(parsed) && parsed.length > 0) {
                    state.history = parsed;
                    // Render lại tất cả tin nhắn đã lưu vào khung chat
                    parsed.forEach(item => {
                        if (item.role === 'user') {
                            renderUserMessageDOM(item.content);
                        } else {
                            renderBotMessageDOM({
                                reply: item.content,
                                data: item.data,
                                intent: item.intent,
                                isOpenAi: item.isOpenAi,
                                model: item.model
                            });
                        }
                    });
                    scrollToBottom();
                }
            }
        } catch (e) {
            console.warn('Lỗi khi khôi phục lịch sử chat:', e);
        }
    }

    function saveSessionHistory() {
        try {
            // Giới hạn lưu tối đa 30 tin nhắn gần nhất để tối ưu bộ nhớ phiên
            const toSave = state.history.slice(-30);
            sessionStorage.setItem(STORAGE_KEY_HISTORY, JSON.stringify(toSave));
        } catch (e) {
            console.warn('Lỗi khi lưu lịch sử chat vào session:', e);
        }
    }

    function restoreChatOpenState() {
        try {
            const isOpen = sessionStorage.getItem(STORAGE_KEY_IS_OPEN) === 'true';
            if (isOpen) {
                state.isOpen = true;
                containerEl.classList.add('active');
                scrollToBottom();
            }
        } catch (e) {
            // ignore
        }
    }

    function toggleChatbot() {
        state.isOpen = !state.isOpen;
        if (state.isOpen) {
            containerEl.classList.add('active');
            inputEl?.focus();
            scrollToBottom();
            sessionStorage.setItem(STORAGE_KEY_IS_OPEN, 'true');
        } else {
            containerEl.classList.remove('active');
            sessionStorage.setItem(STORAGE_KEY_IS_OPEN, 'false');
        }
    }

    async function fetchStatus() {
        try {
            const res = await fetch('/api/chatbot/status');
            if (res.ok) {
                const data = await res.json();
                state.status = data;
                updateStatusUI();
            }
        } catch (e) {
            console.warn('Không thể kết nối trạng thái Chatbot:', e);
        }
    }

    function updateStatusUI() {
        if (!statusTextEl) return;
        if (state.status.isOpenAiConfigured) {
            statusTextEl.innerHTML = `<span class="chatbot-status-dot"></span> Trợ lý AI (${escapeHtml(state.status.model)})`;
        } else {
            statusTextEl.innerHTML = `<span class="chatbot-status-dot" style="background-color: #3B82F6;"></span> Trợ lý G-Trans`;
        }
    }

    async function fetchSuggestions() {
        try {
            const res = await fetch('/api/chatbot/suggestions');
            if (res.ok) {
                const data = await res.json();
                renderSuggestions(data.suggestions || []);
            }
        } catch (e) {
            console.warn('Could not fetch suggestions', e);
        }
    }

    function renderSuggestions(suggestions) {
        if (!suggestionsContainerEl) return;
        suggestionsContainerEl.innerHTML = '';
        if (!suggestions || suggestions.length === 0) return;

        suggestions.forEach(text => {
            const chip = document.createElement('span');
            chip.className = 'chatbot-chip';
            chip.textContent = text;
            chip.addEventListener('click', () => {
                if (inputEl) {
                    inputEl.value = text;
                    sendMessage();
                }
            });
            suggestionsContainerEl.appendChild(chip);
        });
    }

    async function sendMessage() {
        if (!inputEl) return;
        const msg = inputEl.value.trim();
        if (!msg || state.isLoading) return;

        // Hiển thị tin nhắn người dùng
        renderUserMessageDOM(msg);
        inputEl.value = '';

        // Lưu vào state history
        state.history.push({ role: 'user', content: msg });
        saveSessionHistory();

        setLoading(true);

        try {
            // Chỉ gửi 6 lượt trao đổi gần nhất để tiết kiệm token
            const historyPayload = state.history
                .filter(h => h.role === 'user' || h.role === 'assistant')
                .slice(-6)
                .map(h => ({ role: h.role, content: h.content }));

            const response = await fetch('/api/chatbot/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    message: msg,
                    history: historyPayload
                })
            });

            if (!response.ok) {
                throw new Error('Server returned ' + response.status);
            }

            const data = await response.json();
            renderBotMessageDOM(data);

            // Lưu phản hồi bot vào state & sessionStorage
            state.history.push({
                role: 'assistant',
                content: data.reply || '',
                data: data.data,
                intent: data.intent,
                isOpenAi: data.isOpenAi,
                model: data.model
            });
            saveSessionHistory();

            if (data.suggestions && data.suggestions.length > 0) {
                renderSuggestions(data.suggestions);
            }
        } catch (err) {
            console.error('Chat error:', err);
            const errorMsg = '⚠️ Rất tiếc, đã có sự cố khi kết nối máy chủ. Quý khách vui lòng thử lại sau.';
            renderUserMessageDOM(errorMsg, true);
        } finally {
            setLoading(false);
            scrollToBottom();
        }
    }

    function setLoading(isLoading) {
        state.isLoading = isLoading;
        if (typingEl) {
            if (isLoading) typingEl.classList.add('active');
            else typingEl.classList.remove('active');
        }
        if (sendBtnEl) sendBtnEl.disabled = isLoading;
        if (inputEl) inputEl.disabled = isLoading;
        if (!isLoading) inputEl?.focus();
        scrollToBottom();
    }

    function renderUserMessageDOM(text, isError = false) {
        if (!bodyEl) return;
        const msgWrapper = document.createElement('div');
        msgWrapper.className = isError ? 'chatbot-message bot' : 'chatbot-message user';

        const avatar = document.createElement('div');
        avatar.className = 'chatbot-msg-avatar';
        avatar.innerHTML = isError ? '<i class="bi bi-exclamation-triangle-fill text-warning"></i>' : '<i class="bi bi-person-fill"></i>';

        const content = document.createElement('div');
        content.className = 'chatbot-msg-content';
        content.innerHTML = formatMarkdown(text);

        msgWrapper.appendChild(avatar);
        msgWrapper.appendChild(content);

        bodyEl.insertBefore(msgWrapper, typingEl);
        scrollToBottom();
    }

    function renderBotMessageDOM(data) {
        if (!bodyEl) return;
        const msgWrapper = document.createElement('div');
        msgWrapper.className = 'chatbot-message bot';

        const avatar = document.createElement('div');
        avatar.className = 'chatbot-msg-avatar';
        avatar.innerHTML = '<i class="bi bi-robot"></i>';

        const content = document.createElement('div');
        content.className = 'chatbot-msg-content';

        let html = '';
        if (data.isOpenAi) {
            html += `<div class="chatbot-ai-badge"><i class="bi bi-stars"></i> Trả lời bởi OpenAI (${escapeHtml(data.model || 'GPT')})</div>`;
        }

        html += formatMarkdown(data.reply || '');

        // Render card trực quan theo ý định trả về
        if (data.data) {
            if ((data.intent === 'search_trips' || data.intent === 'upcoming_trips') && Array.isArray(data.data)) {
                html += renderTripCards(data.data);
            } else if (data.intent === 'lookup_ticket' && Array.isArray(data.data)) {
                html += renderTicketCards(data.data);
            } else if (data.intent === 'statistics' && data.data.totalRevenue !== undefined) {
                html += renderStatsCard(data.data);
            }
        }

        content.innerHTML = html;

        msgWrapper.appendChild(avatar);
        msgWrapper.appendChild(content);

        bodyEl.insertBefore(msgWrapper, typingEl);
        scrollToBottom();
    }

    function renderTripCards(trips) {
        if (!trips || trips.length === 0) return '';
        let out = '<div class="chatbot-card-list">';
        trips.slice(0, 4).forEach(t => {
            out += `
                <div class="chatbot-trip-card">
                    <div class="chatbot-trip-header">
                        <span class="chatbot-trip-route"><i class="bi bi-geo-alt-fill text-danger me-1"></i>${escapeHtml(t.routeName)}</span>
                        <span class="chatbot-trip-price">${escapeHtml(t.formattedPrice)}</span>
                    </div>
                    <div class="chatbot-trip-details">
                        <div><i class="bi bi-clock me-1 text-primary"></i> ${escapeHtml(t.departureTimeStr)} <span class="badge bg-light text-dark border ms-1">${escapeHtml(t.timeRemaining)}</span></div>
                        <div><i class="bi bi-bus-front me-1 text-secondary"></i> ${escapeHtml(t.busTypeName)} (${escapeHtml(t.licensePlate)})</div>
                        <div><i class="bi bi-check-circle me-1 text-success"></i> Còn <strong>${t.availableSeats}/${t.totalSeats}</strong> chỗ trống</div>
                    </div>
                    <div class="chatbot-trip-action">
                        <a href="${escapeHtml(t.bookingUrl)}" class="chatbot-trip-btn">
                            <i class="bi bi-ticket-perforated"></i> Đặt vé ngay
                        </a>
                    </div>
                </div>
            `;
        });
        out += '</div>';
        return out;
    }

    function renderTicketCards(tickets) {
        if (!tickets || tickets.length === 0) return '';
        let out = '<div class="chatbot-card-list">';
        tickets.slice(0, 3).forEach(t => {
            out += `
                <div class="chatbot-ticket-card">
                    <div class="chatbot-trip-header">
                        <span class="chatbot-trip-route"><i class="bi bi-ticket-detailed-fill text-info me-1"></i>Vé #${t.ticketId} (Đơn #${t.bookingId})</span>
                        <span class="badge ${escapeHtml(t.statusBadgeClass)} chatbot-ticket-badge">${escapeHtml(t.statusDescription)}</span>
                    </div>
                    <div class="chatbot-trip-details">
                        <div><strong>Hành khách:</strong> ${escapeHtml(t.passengerName)} (${escapeHtml(t.passengerPhone)})</div>
                        <div><strong>Tuyến:</strong> ${escapeHtml(t.routeName)}</div>
                        <div><strong>Giờ chạy:</strong> ${escapeHtml(t.departureTimeStr)}</div>
                        <div><strong>Ghế ngồi:</strong> <span class="badge bg-primary">${escapeHtml(t.seatCode)}</span> | Xe: ${escapeHtml(t.licensePlate)}</div>
                    </div>
                    ${t.printTicketUrl ? `
                    <div class="chatbot-trip-action mt-2">
                        <a href="${escapeHtml(t.printTicketUrl)}" target="_blank" class="chatbot-trip-btn" style="background-color: #0284C7;">
                            <i class="bi bi-printer"></i> Xem / In vé
                        </a>
                    </div>` : ''}
                </div>
            `;
        });
        out += '</div>';
        return out;
    }

    function renderStatsCard(stats) {
        let out = `
            <div class="chatbot-stats-card">
                <div class="chatbot-stats-title">
                    <i class="bi bi-graph-up-arrow text-warning"></i> Thống kê ${escapeHtml(stats.periodTitle)}
                </div>
                <div class="chatbot-stats-grid">
                    <div class="chatbot-stat-item">
                        <div class="chatbot-stat-label">Doanh thu</div>
                        <div class="chatbot-stat-val revenue">${escapeHtml(stats.formattedRevenue)}</div>
                    </div>
                    <div class="chatbot-stat-item">
                        <div class="chatbot-stat-label">Vé đã bán</div>
                        <div class="chatbot-stat-val">${stats.totalTicketsSold} vé</div>
                    </div>
                    <div class="chatbot-stat-item">
                        <div class="chatbot-stat-label">Đơn thành công</div>
                        <div class="chatbot-stat-val">${stats.totalPaidBookings}</div>
                    </div>
                    <div class="chatbot-stat-item">
                        <div class="chatbot-stat-label">Tỷ lệ lấp đầy</div>
                        <div class="chatbot-stat-val">${stats.averageOccupancyRate}%</div>
                    </div>
                </div>
        `;

        if (stats.topRoutes && stats.topRoutes.length > 0) {
            out += '<div style="font-size: 11px; color: #CBD5E1; margin-top: 4px;"><strong>Tuyến hàng đầu:</strong><ul style="padding-left: 14px; margin-bottom: 0;">';
            stats.topRoutes.forEach(r => {
                out += `<li>${escapeHtml(r.routeName)}: <strong>${r.ticketsSold}</strong> vé (${escapeHtml(r.formattedRevenue)})</li>`;
            });
            out += '</ul></div>';
        }

        out += '</div>';
        return out;
    }

    function resetChat() {
        state.history = [];
        try {
            sessionStorage.removeItem(STORAGE_KEY_HISTORY);
        } catch (e) {
            // ignore
        }

        // Xóa toàn bộ tin nhắn ngoại trừ thông điệp chào mừng đầu tiên (#chatbotWelcomeMsg)
        const messages = bodyEl.querySelectorAll('.chatbot-message');
        messages.forEach(m => {
            if (m.id !== 'chatbotWelcomeMsg') {
                m.remove();
            }
        });

        fetchSuggestions();
    }

    function scrollToBottom() {
        if (!bodyEl) return;
        setTimeout(() => {
            bodyEl.scrollTop = bodyEl.scrollHeight;
        }, 40);
    }

    function formatMarkdown(text) {
        if (!text) return '';
        let escaped = escapeHtml(text);

        // Chuyển link [text](url) -> <a>
        escaped = escaped.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');

        // Bold **text**
        escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        // Italic *text*
        escaped = escaped.replace(/\*([^*]+)\*/g, '<em>$1</em>');

        // Code `code`
        escaped = escaped.replace(/`([^`]+)`/g, '<code class="bg-light px-1 rounded border text-dark">$1</code>');

        // Xuống dòng
        escaped = escaped.replace(/\n/g, '<br>');

        return escaped;
    }

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
})();
