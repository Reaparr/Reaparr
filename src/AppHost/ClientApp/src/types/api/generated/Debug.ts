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
import { ContentType } from "./http-client";

import type {
  BaseResultDTO,
  PlexMediaType,
  SendTelemetryDiagnosticRequest,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Debug {
  /**
   * No description
   * * @tags Debug
   * @name GetAllLogsEndpoint
   * @request GET:/api/Debug/logs
   * @secure
   */
  getAllLogsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<import("./data-contracts").LiveLogEventDTO[]>({
      url: `/api/Debug/logs`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<import("./data-contracts").LiveLogEventDTO[]>);

  /**
   * No description
   * * @tags Debug
   * @name SendTelemetryDiagnosticEndpoint
   * @request POST:/api/Debug/telemetry/diagnostic
   * @secure
   */
  sendTelemetryDiagnosticEndpoint = (
    data: SendTelemetryDiagnosticRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Debug/telemetry/diagnostic`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Debug
   * @name GetAllUniqueMediaTitlesEndpoint
   * @request GET:/api/Debug/unique-media-titles
   * @secure
   */
  getAllUniqueMediaTitlesEndpoint = (
    query: {
      /** @format int32 */
      count: number;
      type: PlexMediaType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<String[]>({
      url: `/api/Debug/unique-media-titles`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<String[]>);
}

export class DebugPaths {
  static getAllLogsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Debug/logs` });

  static sendTelemetryDiagnosticEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Debug/telemetry/diagnostic` });

  static getAllUniqueMediaTitlesEndpoint = (query: {
    /** @format int32 */
    count: number;
    type: PlexMediaType;
  }) =>
    queryString.stringifyUrl({ url: `/api/Debug/unique-media-titles`, query });
}
