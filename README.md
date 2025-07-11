# SphereSSL

<p align="center">
  <img src="https://github.com/kl3mta3/SphereSSL/blob/master/Images/SphereSSL_ICON.png" alt="SphereSSL Logo" width="300"/>
<h2 align="center">
<b>One cert manager to rule them all, one CA to find them, one browser to bring them all, and in encryption bind them.</b>
</h2>
</p>

<h5 align="center"> Docker Available here <a href ="https://github.com/SphereNetwork/SphereSSL-Docker/releases/tag/1.0.0" > Docker Release</a>. Thanks apples723! </h5>

> **SphereSSL** is a modern, user-friendly certificate manager for Windows, built with ASP.NET.  
> Make SSL certificate creation and renewal effortless for everyone—whether you’re a hobbyist or a professional.  
> Handles both manual and automated DNS challenges, supports multiple DNS providers, and puts the power of SSL in the hands of, well… literally everyone.


---

## Features

- **Fast, Intuitive Dashboard**  
  Simple “Quick Create” flow, certificate management, and helpful links.
- **Auto & Manual DNS**  
  Automated DNS record creation with Cloudflare, AWS, etc.  
  Manual entry for literally any DNS provider.
- **Renewal Scheduling**  
  Toggle auto-renew on/off, get renewal notifications, and never let a cert expire.
- **Multi-User Support**  
  Share certs/orders for collaborative management (future roadmap).
- **Import/Export**  
  Convert, download, or upload certs in your preferred formats.
- **Enterprise Ready**  
  Unlimited domains, no arbitrary limits, tons of provider integrations.

---

## Screenshots


![Dashboard Screenshot](https://github.com/kl3mta3/SphereSSL/blob/master/Images/ssl1.png)
![Add DNS Challenge](https://github.com/kl3mta3/SphereSSL/blob/master/Images/ssl2.png)
![Certificate Details](https://github.com/kl3mta3/SphereSSL/blob/master/Images/ssl4.png)

---

## Windows Installation & Quick Start
“This release will only be available here for a short time. After that, visit Spheressl.com for future versions. Coming soon!!!”
1. **[Download the latest release](https://github.com/SphereNetwork/SphereSSL/releases)** and extract it.
2. **Run SphereSSL.exe** (no complicated setup).
3. **Configure your settings:**  
   - Add domains
   - Choose or add a DNS provider
   - Set up auto-renew (optional)
4. **Request your certificate** and let SphereSSL handle the rest!

> **Tip:** For advanced setup, head to the [Wiki](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL).

---

## Docker Support Now Available!

As of this release, SphereSSL is now fully Docker-compatible!
All Docker development, images, and instructions have moved to our new Docker-specific repo:
Thank you apples723! 

 **[SphereSSL-Docker on GitHub](https://github.com/SphereNetwork/SphereSSL-Docker/releases/latest)**

---

## Quick Start

```bash
git clone https://github.com/SphereNetwork/SphereSSL-Docker.git
cd SphereSSL-Docker
docker-compose up -d --build
```
---

## Supported DNS Providers

- AWS Route53
- Cloudflare
- Cloudns.net
- DigitalOcean
- DNSMadeEasy
- DreamHost
- DuckDNS
- Gandi
- GoDaddy
- Hetzner
- Linode
- Namecheap
- Porkbun
- Vultr
- …and more coming soon!

See [API Credential Requirements](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#api-credential-requirements) for details.

---

## Documentation

- **[Full User Guide & FAQ](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL)**
- [What is SSL?](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#what-is-ssl)
- [What is DNS?](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#what-is-dns)
- [Auto Add Records](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#auto-add-record)
- [Auto Renew](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#auto-renew)
- And much more in the [Wiki](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL)!

---

## Contributing

Pull requests are welcome!  
If you spot a bug or want to add a provider, open an [issue](https://github.com/SphereNetwork/SphereSSL/issues) or submit a PR.  

---

## Roadmap

- [ ] Add more DNS providers
- [ ] Linux & cross-platform support
- [ ] Webhooks & external integrations
- [ ] Fully automated enterprise deployment

*See the [roadmap](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#roadmap) for more.*

---

## License

This project is licensed under the Business Source License 1.1 (BSL-1.1).  
See [LICENSE](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL#license) for details.


---

Yes, the source is open. You can fork it, study it, use it, and self-host for free.
You can absolutely use SphereSSL to manage certificates for your own company, organization, or projects. The only thing you can’t do is turn it into a paid product, paid service, or SaaS for others without permission.

Some folks argue that only MIT/Apache/OSI-approved licenses are “real” open source.
I disagree. “Open source” is about sharing knowledge and empowering builders, not giving SaaS companies a free product to resell 1,000,000 times.

If you want to call it “source-available,” that’s fine. The point is:
You get the code, you get the freedom to use it for anything except commercial exploitation. That’s the trade-off.

---

## Support & Feedback

- **Questions?** [Open an Issue](https://github.com/SphereNetwork/SphereSSL/issues)
- **Feature requests?** We wanna hear ‘em!
- **Need help?** See the [Wiki](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL).

---

> **The more you know...** 

---
