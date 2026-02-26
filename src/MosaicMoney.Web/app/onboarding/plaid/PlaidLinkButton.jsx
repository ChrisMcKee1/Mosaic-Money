"use client";

import React, { useEffect, useCallback } from "react";
import { usePlaidLink } from "react-plaid-link";

export default function PlaidLinkButton({ 
  linkToken, 
  linkSessionId, 
  onSuccess, 
  onExit, 
  onEvent 
}) {
  const handleSuccess = useCallback((public_token, metadata) => {
    onSuccess(public_token, metadata);
  }, [onSuccess]);

  const handleExit = useCallback((err, metadata) => {
    onExit(err, metadata);
  }, [onExit]);

  const handleEvent = useCallback((eventName, metadata) => {
    onEvent(eventName, metadata);
  }, [onEvent]);

  const config = {
    token: linkToken,
    onSuccess: handleSuccess,
    onExit: handleExit,
    onEvent: handleEvent,
  };

  const { open, ready } = usePlaidLink(config);

  return (
    <button 
      onClick={() => open()} 
      disabled={!ready}
      className="bg-black hover:bg-gray-800 text-[var(--color-button-ink)] font-medium py-2 px-4 rounded transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
    >
      Open Plaid Link
    </button>
  );
}