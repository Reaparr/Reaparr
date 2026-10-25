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
  AppCredentialsDTO,
  AppUserLoginEndpointRequest,
  BaseResultDTO,
  UpdateCredentialsEndpointRequest,
  UserClaimsDTO,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Authentication {
  /**
   * No description
   * * @tags Authentication
   * @name GetAppCredentials
   * @summary Gets the username and random password from the main App user.
   * @request GET:/api/Authentication
   * @secure
   */
  getAppCredentials = (params: RequestParams = {}) =>
    axiosObservable<AppCredentialsDTO>({
      url: `/api/Authentication`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<AppCredentialsDTO>);

  /**
   * No description
   * * @tags Authentication
   * @name UpdateCredentialsEndpoint
   * @summary Updates the credentials of a user.
   * @request PUT:/api/Authentication
   * @secure
   */
  updateCredentialsEndpoint = (
    data: UpdateCredentialsEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Authentication`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * @description Logs in a user.
   * * @tags Authentication
   * @name AppUserLoginEndpoint
   * @summary User Login
   * @request POST:/api/Authentication/login
   */
  appUserLoginEndpoint = (
    data: AppUserLoginEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Authentication/login`,
      method: "POST",
      data: data,
      type: ContentType.FormData,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Authentication
   * @name AppUserLogOutEndpoint
   * @request POST:/api/Authentication/logout
   * @secure
   */
  appUserLogOutEndpoint = (params: RequestParams = {}) =>
    axiosObservable<String>({
      url: `/api/Authentication/logout`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<String>);

  /**
   * No description
   * * @tags Authentication
   * @name AuthenticationStatusEndpoint
   * @request GET:/api/Authentication/status
   */
  authenticationStatusEndpoint = (params: RequestParams = {}) =>
    axiosObservable<UserClaimsDTO>({
      url: `/api/Authentication/status`,
      method: "GET",
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<UserClaimsDTO>);
}

export class AuthenticationPaths {
  static getAppCredentials = () =>
    queryString.stringifyUrl({ url: `/api/Authentication` });

  static updateCredentialsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Authentication` });

  static appUserLoginEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Authentication/login` });

  static appUserLogOutEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Authentication/logout` });

  static authenticationStatusEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Authentication/status` });
}
