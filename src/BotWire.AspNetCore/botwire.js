"use strict";(()=>{var o="botwire_session";function v(s){return s.replace(/\\/g,"\\\\").replace(/'/g,"\\'")}function y(s,h,e){let i=h==="bottom-left"?"left":"right";return`
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}

#bubble{
  position:fixed;bottom:24px;${i}:24px;
  width:56px;height:56px;
  background:${s};border:none;border-radius:50%;cursor:pointer;
  display:flex;align-items:center;justify-content:center;
  box-shadow:0 4px 20px rgba(0,0,0,.28);
  transition:transform .2s ease,box-shadow .2s ease;
  z-index:2147483646;color:#fff;
}
#bubble:hover{transform:scale(1.08);box-shadow:0 6px 24px rgba(0,0,0,.32)}
#bubble svg{width:26px;height:26px;fill:#fff;pointer-events:none;flex-shrink:0}

#panel{
  position:fixed;bottom:96px;${i}:20px;
  width:360px;min-height:400px;max-height:560px;
  background:#fff;border-radius:16px;
  box-shadow:0 8px 48px rgba(0,0,0,.18);
  display:flex;flex-direction:column;overflow:hidden;
  z-index:2147483646;
  animation:bw-in .2s ease-out;
}
#panel[hidden]{display:none!important}
@keyframes bw-in{from{transform:scale(.92) translateY(8px);opacity:0}to{transform:none;opacity:1}}

#header{
  display:flex;align-items:center;justify-content:space-between;gap:8px;
  padding:14px 16px;background:${s};color:#fff;flex-shrink:0;
}
#header-title{font-weight:600;font-size:15px}
#close{
  background:none;border:none;color:#fff;cursor:pointer;
  font-size:20px;line-height:1;padding:2px 6px;border-radius:6px;opacity:.75;
}
#close:hover{opacity:1;background:rgba(255,255,255,.15)}

#messages{
  flex:1;overflow-y:auto;padding:12px 12px 4px;
  display:flex;flex-direction:column;gap:8px;
  scroll-behavior:smooth;
}
#messages:empty::before{
  content:'${v(e)}';
  color:#94a3b8;font-size:13px;text-align:center;
  margin:auto;padding:32px 16px;
}

.msg{
  max-width:82%;padding:10px 14px;border-radius:14px;
  font-size:14px;line-height:1.55;word-break:break-word;
  animation:msg-in .15s ease-out;
}
@keyframes msg-in{from{transform:translateY(5px);opacity:0}to{transform:none;opacity:1}}
.msg-user{align-self:flex-end;background:${s};color:#fff;border-bottom-right-radius:3px}
.msg-bot {align-self:flex-start;background:#f1f5f9;color:#1e293b;border-bottom-left-radius:3px}
.msg-sys {
  align-self:center;background:#fef9c3;color:#854d0e;
  font-size:13px;border-radius:8px;text-align:center;max-width:90%;
}

#typing{padding:6px 12px 8px;display:flex;gap:4px;align-items:center;flex-shrink:0}
#typing[hidden]{display:none!important}
#typing span{
  width:7px;height:7px;background:#94a3b8;border-radius:50%;
  animation:bw-dot .9s infinite ease-in-out;
}
#typing span:nth-child(2){animation-delay:.15s}
#typing span:nth-child(3){animation-delay:.3s}
@keyframes bw-dot{0%,80%,100%{transform:scale(.6);opacity:.4}40%{transform:scale(1);opacity:1}}

#input-area{
  display:flex;gap:8px;align-items:flex-end;
  padding:10px 12px;border-top:1px solid #e2e8f0;flex-shrink:0;
}
#input-area[hidden]{display:none!important}
#input{
  flex:1;resize:none;border:1px solid #e2e8f0;border-radius:10px;
  padding:8px 12px;font:inherit;font-size:14px;outline:none;
  max-height:100px;overflow-y:auto;line-height:1.5;
  transition:border-color .15s;
}
#input:focus{border-color:${s};outline:none}
#send{
  background:${s};color:#fff;border:none;border-radius:10px;
  padding:9px 16px;cursor:pointer;font-size:14px;font-weight:500;
  white-space:nowrap;flex-shrink:0;transition:opacity .15s;
}
#send:disabled{opacity:.45;cursor:not-allowed}
#send:not(:disabled):hover{opacity:.88}

#contact-form{
  padding:14px 16px 16px;border-top:1px solid #e2e8f0;
  display:flex;flex-direction:column;gap:10px;flex-shrink:0;
}
#contact-form[hidden]{display:none!important}
#contact-form p{font-size:13px;color:#64748b;line-height:1.5}
#email-input{
  border:1px solid #e2e8f0;border-radius:10px;
  padding:9px 12px;font:inherit;font-size:14px;outline:none;
  transition:border-color .15s;
}
#email-input:focus{border-color:${s}}
#contact-buttons{display:flex;gap:8px}
#contact-submit{
  flex:1;background:${s};color:#fff;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:opacity .15s;
}
#contact-submit:hover{opacity:.88}
#contact-cancel{
  flex:1;background:#f1f5f9;color:#64748b;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:background .15s;
}
#contact-cancel:hover{background:#e2e8f0}

#ticket-card{
  margin:12px 12px 14px;padding:14px 16px;
  background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;
  font-size:13px;color:#166534;text-align:center;line-height:1.55;
  flex-shrink:0;
}
#ticket-card[hidden]{display:none!important}

@media(max-width:480px){
  #panel{bottom:0;left:0;right:0;width:100%;min-height:unset;max-height:100%;
    border-radius:0;border-top-left-radius:16px;border-top-right-radius:16px}
  #bubble{bottom:16px;${i}:16px}
}
`}var b='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2z"/></svg>',w='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2zM6 10h12v2H6v-2zm0-4h12v2H6V6zm8 8H6v-2h8v2z"/></svg>',p=class extends HTMLElement{constructor(){super();this.sessionToken=null;this.streaming=!1;this.awaitingEmail=!1;this.ticketCreated=!1;this.errorOccurred=!1;this.errorMessage="Something went wrong. Please try again.";this.shadow=this.attachShadow({mode:"open"})}get endpoint(){return this.dataset.endpoint??"/support"}get widgetTitle(){return this.dataset.title??"Support"}get primary(){return this.dataset.primaryColor??"#6366f1"}get position(){return this.dataset.position??"bottom-right"}get publicKey(){return this.dataset.publicKey}get placeholder(){return this.dataset.placeholder??"Type a message\u2026"}get contactPrompt(){return this.dataset.contactPrompt??"Please leave your email address so our team can follow up with you."}get emailPlaceholder(){return this.dataset.emailPlaceholder??"your@email.com"}get sendLabel(){return this.dataset.sendLabel??"Send"}get submitLabel(){return this.dataset.submitLabel??"Submit"}get cancelLabel(){return this.dataset.cancelLabel??"Cancel"}get cancelMessage(){return this.dataset.cancelMessage??"You have ended this conversation."}get greeting(){return this.dataset.greeting??"How can we help you today?"}connectedCallback(){this.mount(),this.sessionToken=sessionStorage.getItem(o),this.sessionToken||this.initSession()}mount(){this.shadow.innerHTML=`
<style>${y(this.primary,this.position,this.greeting)}</style>
<button id="bubble" aria-label="Open support chat" aria-expanded="false">${b}</button>
<div id="panel" hidden role="dialog" aria-label="${this.esc(this.widgetTitle)} support chat">
  <div id="header">
    <span id="header-title">${this.esc(this.widgetTitle)}</span>
    <button id="close" aria-label="Close chat">\u2715</button>
  </div>
  <div id="messages" role="log" aria-live="polite" aria-relevant="additions"></div>
  <div id="typing" hidden aria-hidden="true"><span></span><span></span><span></span></div>
  <div id="input-area">
    <textarea id="input" placeholder="${this.esc(this.placeholder)}" rows="1" aria-label="Message input"></textarea>
    <button id="send" type="button">${this.esc(this.sendLabel)}</button>
  </div>
  <form id="contact-form" hidden>
    <p>${this.esc(this.contactPrompt)}</p>
    <input type="email" id="email-input" placeholder="${this.esc(this.emailPlaceholder)}" required aria-label="Email address">
    <div id="contact-buttons">
      <button id="contact-submit" type="submit">${this.esc(this.submitLabel)}</button>
      <button id="contact-cancel" type="button">${this.esc(this.cancelLabel)}</button>
    </div>
  </form>
  <div id="ticket-card" hidden role="status"></div>
</div>`,this.panel=this.q("#panel"),this.bubble=this.q("#bubble"),this.messages=this.q("#messages"),this.typing=this.q("#typing"),this.inputArea=this.q("#input-area"),this.input=this.q("#input"),this.sendBtn=this.q("#send"),this.contact=this.q("#contact-form"),this.emailIn=this.q("#email-input"),this.cancelBtn=this.q("#contact-cancel"),this.ticket=this.q("#ticket-card"),this.bubble.addEventListener("click",()=>this.toggle()),this.q("#close").addEventListener("click",()=>this.close()),this.sendBtn.addEventListener("click",()=>this.handleSend()),this.input.addEventListener("keydown",e=>{e.key==="Enter"&&!e.shiftKey&&(e.preventDefault(),this.handleSend())}),this.input.addEventListener("input",()=>this.autoResize()),this.contact.addEventListener("submit",e=>{e.preventDefault(),this.handleContactSubmit()}),this.cancelBtn.addEventListener("click",()=>this.handleContactCancel())}async initSession(){try{let e=await this.post(`${this.endpoint}/session`,{});if(e.ok){let t=await e.json();this.sessionToken=t.sessionToken,sessionStorage.setItem(o,t.sessionToken),t.errorMessage&&(this.errorMessage=t.errorMessage)}}catch{}}toggle(){this.panel.hidden?this.open():this.ticketCreated?(this.resetConversation(),this.input.focus()):this.close()}open(){this.ticketCreated&&this.resetConversation(),this.panel.hidden=!1,this.bubble.innerHTML=w,this.bubble.setAttribute("aria-expanded","true"),this.awaitingEmail?this.emailIn.focus():this.input.focus()}resetConversation(){this.messages.innerHTML="",this.ticketCreated=!1,this.awaitingEmail=!1,this.streaming=!1,this.errorOccurred=!1,this.contact.hidden=!0,this.ticket.hidden=!0,this.inputArea.hidden=!1,this.sendBtn.disabled=!1,this.emailIn.value="",this.sessionToken=null,sessionStorage.removeItem(o),this.initSession()}close(){this.errorOccurred&&this.resetConversation(),this.panel.hidden=!0,this.bubble.innerHTML=b,this.bubble.setAttribute("aria-expanded","false")}handleSend(){if(this.streaming||this.awaitingEmail)return;let e=this.input.value.trim();e&&(this.input.value="",this.autoResize(),this.appendMessage("user",e),this.stream(e))}handleContactSubmit(){let e=this.emailIn.value.trim();e&&(this.contact.hidden=!0,this.awaitingEmail=!1,this.stream("",e))}handleContactCancel(){this.contact.hidden=!0,this.awaitingEmail=!1,this.ticketCreated=!0,this.appendMessage("sys",this.cancelMessage)}async stream(e,t){if(this.streaming)return;this.streaming=!0,this.sendBtn.disabled=!0,this.typing.hidden=!1,this.sessionToken||await this.initSession();let i={message:e,sessionToken:this.sessionToken};t&&(i.contactEmail=t);let d=null;try{let n=await this.openStream(i);if(!n.ok||!n.body){this.typing.hidden=!0,this.errorOccurred=!0,this.appendMessage("sys",this.errorMessage);return}let m=n.body.getReader(),x=new TextDecoder,a="",c=!1;for(;!c;){let u=await m.read();if(u.done)break;a+=x.decode(u.value,{stream:!0});let l;for(;(l=a.indexOf(`
`))!==-1;){let g=a.slice(0,l);if(a=a.slice(l+1),!g.startsWith("data: "))continue;let f=g.slice(6);if(f==="[DONE]"){c=!0;break}let r;try{r=JSON.parse(f)}catch{continue}switch(this.typing.hidden=!0,r.type){case"token":d||(d=this.appendMessage("bot","")),d.textContent+=r.value,this.scrollBottom();break;case"collect_contact":this.inputArea.hidden=!0,this.contact.hidden=!1,this.awaitingEmail=!0,requestAnimationFrame(()=>{this.scrollBottom(),this.emailIn.focus()});break;case"escalated":this.ticketCreated=!0,this.ticket.hidden=!1,this.ticket.textContent=r.message,this.inputArea.hidden=!0;break;case"blocked":this.appendMessage("sys",r.reason);break}}}}catch(n){this.typing.hidden=!0,n instanceof DOMException&&n.name==="AbortError"||this.appendMessage("sys","Connection error. Please try again.")}finally{this.typing.hidden=!0,this.streaming=!1,!this.awaitingEmail&&!this.ticketCreated&&(this.sendBtn.disabled=!1,this.panel.hidden||this.input.focus())}}async openStream(e){let t=await this.post(`${this.endpoint}/chat/stream`,e);return t.status===400&&await this.isInvalidSession(t)&&(sessionStorage.removeItem(o),this.sessionToken=null,await this.initSession(),this.sessionToken&&(e.sessionToken=this.sessionToken,t=await this.post(`${this.endpoint}/chat/stream`,e))),t}async isInvalidSession(e){try{return(await e.clone().json()).status==="InvalidSession"}catch{return!1}}appendMessage(e,t){let i=document.createElement("div");return i.className=`msg msg-${e}`,i.textContent=t,this.messages.appendChild(i),this.scrollBottom(),i}scrollBottom(){this.messages.scrollTop=this.messages.scrollHeight}autoResize(){this.input.style.height="auto",this.input.style.height=`${Math.min(this.input.scrollHeight,100)}px`}post(e,t){let i={"Content-Type":"application/json"};return this.publicKey&&(i["X-BotWire-Key"]=this.publicKey),fetch(e,{method:"POST",headers:i,body:JSON.stringify(t)})}esc(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}q(e){return this.shadow.querySelector(e)}};customElements.define("botwire-widget",p);})();
