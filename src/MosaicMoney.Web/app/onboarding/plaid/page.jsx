"use client";

import { useState, useCallback } from "react";
import { createLinkToken, logLinkSessionEvent, exchangePublicToken } from "./actions";
import PlaidLinkButton from "./PlaidLinkButton";

export default function PlaidOnboardingPage() {
  const [status, setStatus] = useState("idle"); // idle, loading_token, ready, simulating_link, exchanging_token, success, error
  const [errorMessage, setErrorMessage] = useState(null);
  const [linkSessionId, setLinkSessionId] = useState(null);
  const [linkToken, setLinkToken] = useState(null);
  const [exchangeResult, setExchangeResult] = useState(null);

  const handlePlaidSuccess = useCallback(async (publicToken, metadata) => {
    setStatus("exchanging_token");
    
    // Log SUCCESS event
    await logLinkSessionEvent(linkSessionId, "SUCCESS", "web", metadata);
    
    const institutionId = metadata?.institution?.institution_id || null;
    
    const result = await exchangePublicToken(publicToken, linkSessionId, institutionId, metadata);
    
    if (result.success) {
      setExchangeResult(result.data);
      setStatus("success");
    } else {
      // Log ERROR event
      await logLinkSessionEvent(linkSessionId, "ERROR", "web", { error_message: result.error });
      setErrorMessage(result.error || "Failed to exchange public token");
      setStatus("error");
    }
  }, [linkSessionId]);

  const handlePlaidExit = useCallback(async (err, metadata) => {
    await logLinkSessionEvent(linkSessionId, "EXIT", "web", { error: err, ...metadata });
    if (err) {
      setErrorMessage(err.display_message || err.error_message || "An error occurred in Plaid Link");
      setStatus("error");
    } else {
      setStatus("ready");
    }
  }, [linkSessionId]);

  const handlePlaidEvent = useCallback(async (eventName, metadata) => {
    await logLinkSessionEvent(linkSessionId, eventName, "web", metadata);
  }, [linkSessionId]);

  const handleStart = async () => {
    setStatus("loading_token");
    setErrorMessage(null);

    // In a real app, clientUserId would come from the authenticated user session
    const clientUserId = "demo-user-" + Date.now();

    const result = await createLinkToken(clientUserId);
    
    if (result.success) {
      setLinkToken(result.data.linkToken);
      setLinkSessionId(result.data.linkSessionId);
      setStatus("ready");
    } else {
      setErrorMessage(result.error || "Failed to create link token");
      setStatus("error");
    }
  };

  return (
    <div className="max-w-2xl mx-auto p-6">
      <h1 className="text-2xl font-bold mb-6">Connect Your Bank</h1>
      
      <div className="bg-white shadow rounded-lg p-6 border border-gray-200">
        {status === "idle" && (
          <div className="text-center">
            <p className="mb-4 text-gray-600">
              Securely connect your bank account to import transactions automatically.
            </p>
            <button 
              onClick={handleStart}
              className="bg-blue-600 hover:bg-blue-700 text-[var(--color-button-ink)] font-medium py-2 px-4 rounded transition-colors"
            >
              Get Started
            </button>
          </div>
        )}

        {status === "loading_token" && (
          <div className="text-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-4"></div>
            <p className="text-gray-600">Preparing secure connection...</p>
          </div>
        )}

        {status === "ready" && (
          <div className="text-center">
            <div className="bg-green-50 text-green-800 p-4 rounded mb-6 text-sm">
              Secure session established. Ready to connect.
            </div>
            
            <div className="p-6 rounded-lg mb-6 bg-gray-50">
              <h3 className="font-medium text-gray-700 mb-2">Connect with Plaid</h3>
              <p className="text-sm text-gray-500 mb-4">
                Click below to securely connect your bank account using Plaid.
              </p>
              <div className="flex justify-center gap-4">
                <PlaidLinkButton 
                  linkToken={linkToken}
                  linkSessionId={linkSessionId}
                  onSuccess={handlePlaidSuccess}
                  onExit={handlePlaidExit}
                  onEvent={handlePlaidEvent}
                />
              </div>
            </div>
          </div>
        )}

        {status === "exchanging_token" && (
          <div className="text-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-4"></div>
            <p className="text-gray-600">Securing your connection...</p>
          </div>
        )}

        {status === "success" && (
          <div className="text-center">
            <div className="bg-green-100 text-green-800 p-4 rounded-lg mb-6">
              <svg className="w-12 h-12 mx-auto mb-2 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7"></path>
              </svg>
              <h3 className="text-lg font-bold">Bank Connected Successfully!</h3>
              <p className="text-sm mt-2">Your account is now linked and ready to sync.</p>
            </div>
            
            <div className="text-left bg-gray-50 p-4 rounded border border-gray-200 text-xs overflow-auto">
              <p className="font-semibold mb-2 text-gray-700">Backend Response:</p>
              <pre className="text-gray-600">{JSON.stringify(exchangeResult, null, 2)}</pre>
            </div>
            
            <button 
              onClick={() => setStatus("idle")}
              className="mt-6 bg-gray-200 hover:bg-gray-300 text-gray-800 font-medium py-2 px-4 rounded transition-colors"
            >
              Connect Another Account
            </button>
          </div>
        )}

        {status === "error" && (
          <div className="text-center">
            <div className="bg-red-50 text-red-800 p-4 rounded-lg mb-6">
              <svg className="w-12 h-12 mx-auto mb-2 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              <h3 className="text-lg font-bold">Connection Failed</h3>
              <p className="text-sm mt-2">{errorMessage}</p>
            </div>
            <button 
              onClick={() => setStatus("idle")}
              className="bg-blue-600 hover:bg-blue-700 text-[var(--color-button-ink)] font-medium py-2 px-4 rounded transition-colors"
            >
              Try Again
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
