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

import type { AppUpdateCheckDTO, BaseResultDTO } from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Update {
  /**
   * No description
   * * @tags Update
   * @name CheckForUpdateEndpoint
   * @request GET:/api/Update/Check
   * @secure
   */
  checkForUpdateEndpoint = (params: RequestParams = {}) =>
    axiosObservable<AppUpdateCheckDTO>({
      url: `/api/Update/Check`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<AppUpdateCheckDTO>);

  /**
   * No description
   * * @tags Update
   * @name DownloadUpdateEndpoint
   * @request POST:/api/Update/DownloadUpdate
   * @secure
   */
  downloadUpdateEndpoint = (params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Update/DownloadUpdate`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Update
   * @name ApplyUpdateEndpoint
   * @request POST:/api/Update/execute
   * @secure
   */
  applyUpdateEndpoint = (params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Update/execute`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);
}

export class UpdatePaths {
  static checkForUpdateEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Update/Check` });

  static downloadUpdateEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Update/DownloadUpdate` });

  static applyUpdateEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Update/execute` });
}
