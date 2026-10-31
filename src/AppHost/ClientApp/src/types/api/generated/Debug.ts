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

import type { LiveLogEventDTO, PlexMediaType } from "./data-contracts";

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
    axiosObservable<LiveLogEventDTO[]>({
      url: `/api/Debug/logs`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<LiveLogEventDTO[]>);

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

  static getAllUniqueMediaTitlesEndpoint = (query: {
    /** @format int32 */
    count: number;
    type: PlexMediaType;
  }) =>
    queryString.stringifyUrl({ url: `/api/Debug/unique-media-titles`, query });
}
