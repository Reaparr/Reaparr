/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

import type { RequestParams } from "./http-client";

import type { BrowserTelemetryBootstrapDTO } from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Telemetry {
  /**
   * @description Returns the public browser telemetry configuration and existing application account identity, or null when telemetry is unconfigured.
   * * @tags Telemetry
   * @name GetBrowserTelemetryBootstrapEndpoint
   * @summary Gets the browser telemetry bootstrap configuration.
   * @request GET:/api/Telemetry/bootstrap
   */
  getBrowserTelemetryBootstrapEndpoint = (params: RequestParams = {}) =>
    axiosObservable<BrowserTelemetryBootstrapDTO>({
      url: `/api/Telemetry/bootstrap`,
      method: "GET",
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BrowserTelemetryBootstrapDTO>);
}

export class TelemetryPaths {
  static getBrowserTelemetryBootstrapEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Telemetry/bootstrap` });
}
