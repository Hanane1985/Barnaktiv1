"use client";

import { FormEvent, useEffect, useRef, useState } from "react";

import {
  askActivityAssistant,
  type ActivityAiSource,
} from "@/lib/ai-assistant";

type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: ActivityAiSource[];
  errorMessage?: string;
};

const starterPrompts = [
  "Vad kan en 8-åring göra i Göteborg nästa helg?",
  "Finns det gratis fotboll för barn runt 10 år?",
  "Simning eller dans för 6–7 år i Göteborg",
];

export function ChatWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, isOpen, isLoading]);

  useEffect(() => {
    if (isOpen) {
      textareaRef.current?.focus();
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isOpen]);

  async function submitQuestion(question: string) {
    const trimmed = question.trim();
    if (!trimmed || isLoading) {
      return;
    }

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      content: trimmed,
    };

    setMessages((current) => [...current, userMessage]);
    setInput("");
    setIsLoading(true);

    const result = await askActivityAssistant(trimmed);

    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "assistant",
      content: result.errorMessage
        ? "Assistenten kunde inte svara just nu."
        : result.answer || "Jag hittade inget att svara på.",
      sources: result.sources,
      errorMessage: result.errorMessage,
    };

    setMessages((current) => [...current, assistantMessage]);
    setIsLoading(false);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitQuestion(input);
  }

  function handleTextareaKeyDown(
    event: React.KeyboardEvent<HTMLTextAreaElement>,
  ) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void submitQuestion(input);
    }
  }

  function resetChat() {
    if (isLoading) {
      return;
    }
    setMessages([]);
    setInput("");
    textareaRef.current?.focus();
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        aria-expanded={isOpen}
        aria-controls="barnaktiv-chat-panel"
        aria-label={isOpen ? "Stäng chatten" : "Öppna chatten"}
        className="fixed bottom-5 right-5 z-50 flex h-14 w-14 items-center justify-center rounded-full bg-[color:var(--accent)] text-white shadow-xl transition hover:scale-105 hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-[color:var(--accent)] focus:ring-offset-2 sm:bottom-6 sm:right-6"
      >
        {isOpen ? (
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="h-6 w-6"
            aria-hidden
          >
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        ) : (
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="h-6 w-6"
            aria-hidden
          >
            <path d="M21 12a8 8 0 0 1-11.6 7.13L4 20l1-4.4A8 8 0 1 1 21 12z" />
            <path d="M8.5 11.5h.01M12 11.5h.01M15.5 11.5h.01" />
          </svg>
        )}
      </button>

      {isOpen ? (
        <div
          id="barnaktiv-chat-panel"
          role="dialog"
          aria-modal="false"
          aria-label="AI-assistent för barnaktiviteter"
          className="fixed bottom-24 right-3 z-50 flex max-h-[min(78vh,640px)] w-[min(calc(100vw-1.5rem),24rem)] flex-col overflow-hidden rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface-strong)] shadow-2xl sm:right-6 sm:w-96"
        >
          <header className="flex items-start justify-between gap-3 border-b border-[color:var(--border)] bg-[color:var(--surface)] px-4 py-3">
            <div>
              <p className="text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-[color:var(--accent)]">
                AI-assistent
              </p>
              <h2 className="font-display text-base font-semibold text-[color:var(--foreground)]">
                Fråga om barnaktiviteter
              </h2>
            </div>
            <div className="flex items-center gap-1">
              {messages.length > 0 ? (
                <button
                  type="button"
                  onClick={resetChat}
                  disabled={isLoading}
                  aria-label="Starta ny chatt"
                  title="Starta ny chatt"
                  className="flex items-center gap-1 rounded-full px-2 py-1 text-xs font-medium text-[color:var(--muted)] transition hover:bg-[color:var(--surface-strong)] hover:text-[color:var(--foreground)] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className="h-3.5 w-3.5"
                    aria-hidden
                  >
                    <path d="M3 12a9 9 0 1 0 3-6.7" />
                    <path d="M3 4v5h5" />
                  </svg>
                  Ny chatt
                </button>
              ) : null}
              <button
                type="button"
                onClick={() => setIsOpen(false)}
                aria-label="Stäng chatten"
                className="rounded-full p-1.5 text-[color:var(--muted)] transition hover:bg-[color:var(--surface-strong)] hover:text-[color:var(--foreground)]"
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="h-4 w-4"
                  aria-hidden
                >
                  <path d="M18 6 6 18M6 6l12 12" />
                </svg>
              </button>
            </div>
          </header>

          <div className="flex-1 overflow-y-auto px-3 py-3 sm:px-4">
            {messages.length === 0 ? (
              <div className="space-y-3">
                <p className="text-sm leading-relaxed text-[color:var(--muted)]">
                  Beskriv vad ni söker i vanlig svenska. Jag söker i Barnaktivs databas
                  och svarar med förslag du kan klicka vidare på.
                </p>
                <div className="flex flex-col gap-2">
                  {starterPrompts.map((prompt) => (
                    <button
                      key={prompt}
                      type="button"
                      disabled={isLoading}
                      onClick={() => void submitQuestion(prompt)}
                      className="rounded-xl border border-[color:var(--border)] bg-[color:var(--surface)] px-3 py-2 text-left text-xs text-[color:var(--foreground)] transition hover:border-[color:var(--accent)] disabled:opacity-60 sm:text-sm"
                    >
                      {prompt}
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              <div className="space-y-3">
                {messages.map((message) => (
                  <article
                    key={message.id}
                    className={`rounded-2xl border px-3 py-2.5 text-sm ${
                      message.role === "user"
                        ? "ml-6 border-[color:var(--accent)]/30 bg-[color:var(--accent-soft)]"
                        : "mr-6 border-[color:var(--border)] bg-[color:var(--surface)]"
                    }`}
                  >
                    <p className="text-[0.6rem] font-semibold uppercase tracking-wide text-[color:var(--muted)]">
                      {message.role === "user" ? "Du" : "Barnaktiv"}
                    </p>
                    <p className="mt-1.5 whitespace-pre-wrap leading-relaxed">
                      {message.content}
                    </p>

                    {message.errorMessage ? (
                      <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                        {message.errorMessage}
                      </p>
                    ) : null}

                    {message.sources && message.sources.length > 0 ? (
                      <ul className="mt-3 space-y-2 border-t border-[color:var(--border)] pt-2">
                        {message.sources.map((source) => (
                          <li key={source.id}>
                            {source.signupUrl ? (
                              <a
                                href={source.signupUrl}
                                target="_blank"
                                rel="noreferrer"
                                className="text-xs font-medium text-[color:var(--accent)] hover:underline sm:text-sm"
                              >
                                {source.title}
                              </a>
                            ) : (
                              <span className="text-xs font-medium sm:text-sm">
                                {source.title}
                              </span>
                            )}
                            <p className="text-[0.7rem] text-[color:var(--muted)]">
                              {[source.city, formatDateLabel(source.date)]
                                .filter(Boolean)
                                .join(" · ")}
                            </p>
                          </li>
                        ))}
                      </ul>
                    ) : null}
                  </article>
                ))}

                {isLoading ? (
                  <p
                    className="mr-6 rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface)] px-3 py-2 text-xs text-[color:var(--muted)]"
                    aria-live="polite"
                  >
                    Assistenten tänker…
                  </p>
                ) : null}

                <div ref={messagesEndRef} />
              </div>
            )}
          </div>

          <form
            onSubmit={handleSubmit}
            className="border-t border-[color:var(--border)] bg-[color:var(--surface)] p-3"
          >
            <label htmlFor="chat-widget-input" className="sr-only">
              Din fråga
            </label>
            <textarea
              id="chat-widget-input"
              ref={textareaRef}
              rows={2}
              value={input}
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={handleTextareaKeyDown}
              placeholder="Skriv en fråga…"
              maxLength={500}
              disabled={isLoading}
              className="w-full resize-none rounded-xl border border-[color:var(--border)] bg-[color:var(--background)] px-3 py-2 text-sm text-[color:var(--foreground)] outline-none ring-[color:var(--accent)] focus:ring-2 disabled:opacity-60"
            />
            <div className="mt-2 flex items-center justify-between gap-2">
              <p className="text-[0.65rem] text-[color:var(--muted)]">
                {input.length}/500
              </p>
              <button
                type="submit"
                disabled={isLoading || input.trim().length === 0}
                className="rounded-full bg-[color:var(--accent)] px-4 py-1.5 text-xs font-semibold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50 sm:text-sm"
              >
                {isLoading ? "Tänker…" : "Fråga"}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </>
  );
}

function formatDateLabel(value: string) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toLocaleDateString("sv-SE", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
