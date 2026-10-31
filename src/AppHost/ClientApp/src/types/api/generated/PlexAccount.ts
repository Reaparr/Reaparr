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
  CreatePlexAccountEndpointRequest,
  GeneratePlexTokenResponse,
  PlexAccountDTO,
  RefreshPlexAccountAccessRapportDTO,
  ValidatePlexCredentialsDTO,
  ValidatePlexCredentialsEndpointRequest,
  ValidatePlexTokenEndpointRequest,
  ValidatePlexTokenEndpointResponse,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class PlexAccount {
  /**
   * No description
   * * @tags Plexaccount
   * @name CreatePlexAccountEndpoint
   * @request POST:/api/PlexAccount
   * @secure
   */
  createPlexAccountEndpoint = (
    data: CreatePlexAccountEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexAccountDTO>({
      url: `/api/PlexAccount`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexAccountDTO>);

  /**
   * No description
   * * @tags Plexaccount
   * @name GetAllPlexAccountsEndpoint
   * @request GET:/api/PlexAccount
   * @secure
   */
  getAllPlexAccountsEndpoint = (
    query?: {
      enabledOnly?: boolean;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexAccountDTO[]>({
      url: `/api/PlexAccount`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexAccountDTO[]>);

  /**
   * No description
   * * @tags Plexaccount
   * @name UpdatePlexAccountByIdEndpoint
   * @request PUT:/api/PlexAccount
   * @secure
   */
  updatePlexAccountByIdEndpoint = (
    data: PlexAccountDTO | null,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexAccountDTO>({
      url: `/api/PlexAccount`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexAccountDTO>);

  /**
   * No description
   * * @tags Plexaccount
   * @name IsUsernameAvailableEndpoint
   * @request GET:/api/PlexAccount/check
   * @secure
   */
  isUsernameAvailableEndpoint = (
    query: {
      username: string;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<Boolean>({
      url: `/api/PlexAccount/check`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<Boolean>);

  /**
   * No description
   * * @tags Plexaccount
   * @name GeneratePlexTokenEndpoint
   * @request GET:/api/PlexAccount/generate-token/{plexAccountId}
   * @secure
   */
  generatePlexTokenEndpoint = (
    plexAccountId: number,
    query?: {
      verificationCode?: string;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<GeneratePlexTokenResponse>({
      url: `/api/PlexAccount/generate-token/${plexAccountId}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<GeneratePlexTokenResponse>);

  /**
   * No description
   * * @tags Plexaccount
   * @name RefreshPlexAccountAccessEndpoint
   * @request GET:/api/PlexAccount/refresh/{plexAccountId}
   * @secure
   */
  refreshPlexAccountAccessEndpoint = (
    plexAccountId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<RefreshPlexAccountAccessRapportDTO[]>({
      url: `/api/PlexAccount/refresh/${plexAccountId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RefreshPlexAccountAccessRapportDTO[]>);

  /**
   * No description
   * * @tags Plexaccount
   * @name ValidatePlexCredentialsEndpoint
   * @request POST:/api/PlexAccount/validate/credentials
   * @secure
   */
  validatePlexCredentialsEndpoint = (
    data: ValidatePlexCredentialsEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<ValidatePlexCredentialsDTO>({
      url: `/api/PlexAccount/validate/credentials`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<ValidatePlexCredentialsDTO>);

  /**
   * No description
   * * @tags Plexaccount
   * @name ValidatePlexTokenEndpoint
   * @request POST:/api/PlexAccount/validate/token
   * @secure
   */
  validatePlexTokenEndpoint = (
    data: ValidatePlexTokenEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<ValidatePlexTokenEndpointResponse>({
      url: `/api/PlexAccount/validate/token`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<ValidatePlexTokenEndpointResponse>);

  /**
   * No description
   * * @tags Plexaccount
   * @name DeletePlexAccountByIdEndpoint
   * @request DELETE:/api/PlexAccount/{plexAccountId}
   * @secure
   */
  deletePlexAccountByIdEndpoint = (
    plexAccountId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexAccount/${plexAccountId}`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexaccount
   * @name GetPlexAccountByIdEndpoint
   * @request GET:/api/PlexAccount/{plexAccountId}
   * @secure
   */
  getPlexAccountByIdEndpoint = (
    plexAccountId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexAccountDTO>({
      url: `/api/PlexAccount/${plexAccountId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexAccountDTO>);
}

export class PlexAccountPaths {
  static createPlexAccountEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexAccount` });

  static getAllPlexAccountsEndpoint = (query?: { enabledOnly?: boolean }) =>
    queryString.stringifyUrl({ url: `/api/PlexAccount`, query });

  static updatePlexAccountByIdEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexAccount` });

  static isUsernameAvailableEndpoint = (query: { username: string }) =>
    queryString.stringifyUrl({ url: `/api/PlexAccount/check`, query });

  static generatePlexTokenEndpoint = (
    plexAccountId: number,
    query?: {
      verificationCode?: string;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexAccount/generate-token/${plexAccountId}`,
      query,
    });

  static refreshPlexAccountAccessEndpoint = (plexAccountId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexAccount/refresh/${plexAccountId}`,
    });

  static validatePlexCredentialsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexAccount/validate/credentials` });

  static validatePlexTokenEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexAccount/validate/token` });

  static deletePlexAccountByIdEndpoint = (plexAccountId: number) =>
    queryString.stringifyUrl({ url: `/api/PlexAccount/${plexAccountId}` });

  static getPlexAccountByIdEndpoint = (plexAccountId: number) =>
    queryString.stringifyUrl({ url: `/api/PlexAccount/${plexAccountId}` });
}
