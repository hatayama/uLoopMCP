/**
 * Enhanced Service Locator Pattern with Lifecycle Management
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#ServiceLocatorEnhancement
 *
 * Related classes:
 * - ApplicationService implementations (application/services/)
 * - UseCase implementations (domain/use-cases/)
 * - Infrastructure layer classes (infrastructure/)
 *
 * Key improvements:
 * - Type-safe service tokens using Symbols
 * - Lifecycle management (singleton vs transient)
 * - Circular dependency detection
 * - Enhanced testing support with mocking
 */

import { ServiceToken } from './service-tokens.js';
import { ServiceResolutionError } from './errors.js';

export type ServiceLifecycle = 'singleton' | 'transient';

interface ServiceRegistration<T> {
  factory: () => T;
  lifecycle: ServiceLifecycle;
  instance?: T; // Singleton instance cache
}

/**
 * Enhanced Service Locator with type safety and lifecycle management
 *
 * Responsibilities:
 * - Type-safe dependency registration and resolution
 * - Lifecycle management (singleton/transient)
 * - Circular dependency detection
 * - Test support with mocking capabilities
 * - Performance optimization with singleton caching
 */
export class ServiceLocator {
  private static services: Map<symbol, ServiceRegistration<unknown>> = new Map();
  private static resolutionStack: Set<symbol> = new Set();
  private static originalServices: Map<symbol, ServiceRegistration<unknown>> = new Map();

  /**
   * Register service with lifecycle management
   *
   * @param token Type-safe service token
   * @param factory Factory function to create service instance
   * @param lifecycle Service lifecycle (singleton or transient)
   */
  static register<T>(
    token: ServiceToken<T>,
    factory: () => T,
    lifecycle: ServiceLifecycle = 'transient',
  ): void {
    this.services.set(token, { factory, lifecycle });
  }

  /**
   * Resolve service instance with circular dependency detection
   *
   * @param token Type-safe service token
   * @returns Service instance
   * @throws Error if service not registered or circular dependency detected
   */
  static resolve<T>(token: ServiceToken<T>): T {
    // Circular dependency detection
    if (this.resolutionStack.has(token)) {
      const stackArray = Array.from(this.resolutionStack).map((s) => s.toString());
      throw new ServiceResolutionError(
        `Circular dependency detected: ${stackArray.join(' -> ')} -> ${token.toString()}`,
        token.toString(),
        stackArray,
      );
    }

    this.resolutionStack.add(token);
    try {
      const registration = this.services.get(token);
      if (!registration) {
        throw new ServiceResolutionError(
          `Service not registered: ${token.toString()}`,
          token.toString(),
        );
      }

      // Singleton: return cached instance or create and cache
      if (registration.lifecycle === 'singleton') {
        if (!registration.instance) {
          registration.instance = registration.factory();
        }
        return registration.instance as T;
      }

      // Transient: create new instance every time
      return registration.factory() as T;
    } finally {
      this.resolutionStack.delete(token);
    }
  }

  /**
   * Clear all service registrations
   * Primarily used for testing
   */
  static clear(): void {
    this.services.clear();
    this.resolutionStack.clear();
    this.originalServices.clear();
  }

  /**
   * Get list of registered services
   * Primarily used for debugging
   */
  static getRegisteredServices(): string[] {
    return Array.from(this.services.keys()).map((token) => token.toString());
  }

  /**
   * Check if service is registered
   *
   * @param token Service token
   * @returns true if registered
   */
  static isRegistered<T>(token: ServiceToken<T>): boolean {
    return this.services.has(token);
  }

  /**
   * Mock service for testing (temporarily replace)
   *
   * @param token Service token to mock
   * @param mockFactory Mock factory function
   */
  static mock<T>(token: ServiceToken<T>, mockFactory: () => T): void {
    // Backup original service if exists
    const original = this.services.get(token);
    if (original) {
      this.originalServices.set(token, original);
    }

    this.register(token, mockFactory, 'transient');
  }

  /**
   * Restore mocked service to original
   *
   * @param token Service token to restore
   */
  static restore<T>(token: ServiceToken<T>): void {
    const original = this.originalServices.get(token);
    if (original) {
      this.services.set(token, original);
      this.originalServices.delete(token);
    } else {
      this.services.delete(token);
    }
  }

  /**
   * Restore all mocked services (cleanup after tests)
   */
  static restoreAll(): void {
    for (const [token, original] of this.originalServices) {
      this.services.set(token, original);
    }
    this.originalServices.clear();
  }

  /**
   * Get service registration info (for debugging)
   */
  static getServiceInfo<T>(token: ServiceToken<T>): {
    isRegistered: boolean;
    lifecycle?: ServiceLifecycle;
    hasInstance?: boolean;
  } {
    const registration = this.services.get(token);
    if (!registration) {
      return { isRegistered: false };
    }

    return {
      isRegistered: true,
      lifecycle: registration.lifecycle,
      hasInstance: registration.lifecycle === 'singleton' && !!registration.instance,
    };
  }
}
