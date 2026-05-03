# SphereSSL-Docker

<p align="center">
  <img src="https://github.com/kl3mta3/SphereSSL/blob/master/Images/SphereSSL_ICON.png" alt="SphereSSL Logo" width="300"/>
<h2 align="center">
<b>One cert manager to rule them all, one CA to find them, one browser to bring them all, and in encryption bind them.</b>
</h2>
</p>

Thanks goes to Thanks [@apples723](https://github.com/apples723)!
Get the Image here- <a href = "https://hub.docker.com/r/kl3mta3/spheressl"> Docker Hub<a/>

> **SphereSSL** is a modern, user-friendly certificate manager, built with ASP.NET.  
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

## Installation & Quick Start
“This release will only be available here for a short time. After that, visit Spheressl.com for future versions. Coming soon!!!”

# Option 1- Build Locally

# Build the image
docker build -t spheressl .

# Run the container with persistent storage
docker run -d \
  -p 7171:7171 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/certs:/app/certs \
  -v $(pwd)/logs:/app/logs \
  --name spheressl \
  --restart unless-stopped \
  spheressl

# Option 2- Run from Docker Hub

# Pull the latest image
docker pull kl3mta3/spheressl:latest

# Or just run directly (no local build needed!)
docker run -d \
  -p 7171:7171 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/certs:/app/certs \
  -v $(pwd)/logs:/app/logs \
  --name spheressl \
  --restart unless-stopped \
  kl3mta3/spheressl:latest

 **Request your certificate** and let SphereSSL handle the rest!

> **Tip:** For advanced setup, head to the [Wiki](https://github.com/SphereNetwork/SphereSSL/wiki/SphereSSL).

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
