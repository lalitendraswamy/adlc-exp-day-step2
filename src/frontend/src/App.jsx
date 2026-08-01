import React, { useState } from 'react';
import { getApiBaseUrl, readProblemMessage, toApiUrl } from './lib/api.js';

const DEFAULT_CURRENCY = 'USD';

function normalizeCurrency(code) {
  return String(code ?? '').trim().toUpperCase();
}

function isValidCurrency(code) {
  return /^[A-Z]{3}$/.test(code);
}

function round2(n) {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}

export default function App() {
  const [apiBaseUrl] = useState(() => getApiBaseUrl());

  const [amount, setAmount] = useState('100.00');
  const [fromCurrency, setFromCurrency] = useState(DEFAULT_CURRENCY);
  const [toCurrency, setToCurrency] = useState('EUR');

  const [submitting, setSubmitting] = useState(false);
  const [conversionError, setConversionError] = useState('');
  const [conversionResult, setConversionResult] = useState(null);

  const [lookupAuditId, setLookupAuditId] = useState('');
  const [lookupError, setLookupError] = useState('');
  const [lookupResult, setLookupResult] = useState(null);
  const [lookupLoading, setLookupLoading] = useState(false);

  async function onConvert(e) {
    e.preventDefault();
    setConversionError('');
    setConversionResult(null);

    const normalizedFrom = normalizeCurrency(fromCurrency);
    const normalizedTo = normalizeCurrency(toCurrency);

    const amountNum = Number(amount);
    if (!Number.isFinite(amountNum) || amountNum <= 0) {
      setConversionError('Amount must be a positive number.');
      return;
    }
    if (!isValidCurrency(normalizedFrom) || !isValidCurrency(normalizedTo)) {
      setConversionError('Currency codes must be uppercase 3-letter ISO codes (e.g. USD, EUR).');
      return;
    }

    setSubmitting(true);
    try {
      const res = await fetch(
        toApiUrl(apiBaseUrl, '/api/conversions'),
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            amount: amountNum,
            fromCurrency: normalizedFrom,
            toCurrency: normalizedTo
          })
        }
      );

      if (!res.ok) {
        setConversionError(await readProblemMessage(res));
        return;
      }

      const data = await res.json();
      setConversionResult({
        ...data,
        // UI rounding so display matches backend even if JSON has more precision.
        convertedAmount: round2(Number(data.convertedAmount))
      });
    } catch (err) {
      setConversionError('Currency conversion failed. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  async function onLookup(e) {
    e.preventDefault();
    setLookupError('');
    setLookupResult(null);

    const id = String(lookupAuditId ?? '').trim();
    if (!id) {
      setLookupError('Audit ID is required.');
      return;
    }

    setLookupLoading(true);
    try {
      const res = await fetch(toApiUrl(apiBaseUrl, `/api/conversions/${encodeURIComponent(id)}`));
      if (!res.ok) {
        setLookupError(await readProblemMessage(res));
        return;
      }

      const data = await res.json();
      setLookupResult({
        ...data,
        convertedAmount: round2(Number(data.convertedAmount))
      });
    } catch {
      setLookupError('Audit lookup failed. Please try again.');
    } finally {
      setLookupLoading(false);
    }
  }

  return (
    <div className="container">
      <div className="card">
        <h1 style={{ marginTop: 0, marginBottom: 8 }}>Real-Time Currency Conversion</h1>
        <div className="muted">Submit a conversion to immediately persist a reconstructable audit record.</div>
      </div>

      <div className="card">
        <form onSubmit={onConvert}>
          <div className="grid two">
            <div className="field">
              <label>Amount</label>
              <input
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="100.00"
              />
            </div>
            <div className="field">
              <label>Source Currency (from)</label>
              <input
                value={fromCurrency}
                onChange={(e) => setFromCurrency(e.target.value)}
                placeholder="USD"
              />
            </div>
            <div className="field">
              <label>Target Currency (to)</label>
              <input
                value={toCurrency}
                onChange={(e) => setToCurrency(e.target.value)}
                placeholder="EUR"
              />
            </div>
            <div className="field" style={{ justifyContent: 'flex-end' }}>
              <label>&nbsp;</label>
              <button type="submit" disabled={submitting}>
                {submitting ? 'Converting…' : 'Convert'}
              </button>
            </div>
          </div>
        </form>

        {conversionError ? <div className="error" style={{ marginTop: 12 }}>{conversionError}</div> : null}

        {conversionResult ? (
          <div className="success" style={{ marginTop: 12 }}>
            <div><strong>Converted Amount:</strong> {conversionResult.convertedAmount}</div>
            <div><strong>Rate:</strong> {conversionResult.rate}</div>
            <div><strong>Audit ID:</strong> {conversionResult.auditId}</div>
            <div><strong>Executed At (UTC):</strong> {conversionResult.executedAtUtc}</div>
          </div>
        ) : null}
      </div>

      <div className="card">
        <form onSubmit={onLookup}>
          <div className="field">
            <label>Audit lookup (paste audit ID)</label>
            <input
              value={lookupAuditId}
              onChange={(e) => setLookupAuditId(e.target.value)}
              placeholder="e.g. 7f2d3b2e-..."
            />
          </div>
          <div className="row" style={{ marginTop: 10 }}>
            <button type="submit" disabled={lookupLoading}>
              {lookupLoading ? 'Looking up…' : 'Fetch audit record'}
            </button>
            <div className="muted">Returns the persisted conversion details without recomputing the rate.</div>
          </div>
        </form>

        {lookupError ? <div className="error" style={{ marginTop: 12 }}>{lookupError}</div> : null}

        {lookupResult ? (
          <div className="success" style={{ marginTop: 12 }}>
            <pre>{JSON.stringify(lookupResult, null, 2)}</pre>
          </div>
        ) : null}
      </div>
    </div>
  );
}
