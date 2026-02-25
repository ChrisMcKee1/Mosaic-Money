"use client";

import { useState } from "react";
import { createLinkToken, logLinkSessionEvent, exchangePublicToken } from "./actions";

export default function PlaidOnboardingPage() {
  const [status, setStatus] = useState("idle"); // idle, loading_token, ready, simulating_link, exchanging_token, success, error
  const [errorMessage, setErrorMessage] = useState(null);
  const [linkSessionId, setLinkSessionId] = useState(null);
  const [linkToken, setLinkToken] = useState(null);
  const [exchangeResult, setExchangeResult] = useState(null);

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

  const handleOpenLink = async () => {
    setStatus("simulating_link");
    
    // Log OPEN event
    await logLinkSessionEvent(linkSessionId, "OPEN", "web_demo");

    // Simulate user interacting with Plaid Link
    setTimeout(async () => {
      // Simulate SUCCESS event
      await logLinkSessionEvent(linkSessionId, "SUCCESS", "web_demo", { institution_name: "Chase" });
      
      // Simulate receiving a public token from Plaid Link onSuccess callback
      const simulatedPublicToken = "public-sandbox-" + crypto.randomUUID();
      const simulatedInstitutionId = "ins_1"; // Chase
      
      handleExchange(simulatedPublicToken, simulatedInstitutionId);
    }, 2000);
  };

  const handleCancelLink = async () => {
    await logLinkSessionEvent(linkSessionId, "EXIT", "web_demo", { reason: "user_cancelled" });
    setStatus("ready");
  };

  const handleExchange = async (publicToken, institutionId) => {
    setStatus("exchanging_token");
    
    const result = await exchangePublicToken(publicToken, linkSessionId, institutionId);
    
    if (result.success) {
      setExchangeResult(result.data);
      setStatus("success");
    } else {
      // Log ERROR event
      await logLinkSessionEvent(linkSessionId, "ERROR", "web_demo", { error_message: result.error });
      setErrorMessage(result.error || "Failed to exchange public token");
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
            
            {/* DEFERRED: Real Plaid SDK integration */}
            <div className="border-2 border-dashed border-gray-300 p-6 rounded-lg mb-6 bg-gray-50">
              <h3 className="font-medium text-gray-700 mb-2">Demo Mode: Simulated Plaid Link</h3>
              <p className="text-sm text-gray-500 mb-4">
                The real Plaid SDK is deferred. Clicking below will simulate a successful bank connection 
                using the backend contracts.
              </p>
              <div className="flex justify-center gap-4">
                <button 
                  onClick={handleCancelLink}
                  className="bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 font-medium py-2 px-4 rounded transition-colors"
                >
                  Simulate Cancel
                </button>
                <button 
                  onClick={handleOpenLink}
                  className="bg-black hover:bg-gray-800 text-[var(--color-button-ink)] font-medium py-2 px-4 rounded transition-colors"
                >
                  Simulate Success
                </button>
              </div>
            </div>
          </div>
        )}

        {status === "simulating_link" && (
          <div className="text-center py-8">
            <div className="animate-pulse flex space-x-4 justify-center mb-4">
              <div className="h-3 w-3 bg-gray-400 rounded-full"></div>
              <div className="h-3 w-3 bg-gray-400 rounded-full"></div>
              <div className="h-3 w-3 bg-gray-400 rounded-full"></div>
            </div>
            <p className="text-gray-600">User is interacting with Plaid Link...</p>
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
